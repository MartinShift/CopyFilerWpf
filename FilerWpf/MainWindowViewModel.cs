using Microsoft.Win32;
using My.BaseViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FolderBrowserEx;
using System.Drawing;
using static System.Windows.Forms.AxHost;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace FilerWpf
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        //Copying
        public MainWindowViewModel()
        {
            Files = new()
            {
                "D:\\musichka"
            };
            To = "G:\\";
            Progresses = new();
            AllThreads = 0;
        }
        public int Selected { get; set; }
        //Thread
        public int AllThreads { get; set; }
        public int numThreads { get; set; }
        //Progress
        public ObservableCollection<FileProgressBar> ProgressViews { get => new(Progresses); }
        public List<FileProgressBar> Progresses { get; set; }
        //General bar
        public string Progressstr { get => TotalFileCount == 0 ? "" : $"{CopiedFileCount} / {TotalFileCount} Files copied"; }
        public double ProgressesAvg { get => Progresses.Count == 0 ? 0 : Progresses.Average(x => x.ProgressValue); }
        public CopyState GeneralState { get; set; }
        public System.Windows.Media.Brush ButtonBrush { get => GeneralState == CopyState.Working ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Blue); }
        public string BarContent { get => GeneralState == CopyState.Working ? "Pause" : "Resume"; }
        //File
        public int TotalFileCount { get; set; }
        public int CopiedFileCount { get; set; }
        public List<string> Files { get; set; }
        public string To { get; set; }
        //Copy
        private void CopyDirectories()
        {
            // Create an array of progress indicators and reset events, one for each thread
            if (numThreads > Files.Count)
            {
                numThreads = Math.Min(numThreads, Files.Count);
            }
            else numThreads = Files.Count;
            AllThreads += numThreads;
            for (int i = 0; i < numThreads; i++)
            {
                Progresses.Add(new FileProgressBar()
                {
                    Progress = new Progress<double>(progress =>
                    {
                        // Report progress for the current thread
                        OnPropertyChanged(nameof(ProgressViews));
                    }),
                    CurrentFile = "",
                    ProgressValue = 0,
                    resetEvent = new ManualResetEventSlim(true)
                });
            }

            // Create a list of tuples to store the source and destination directories for each copy operation
            var copyOperations = new List<(string source, string destination)>();

            // Iterate over each directory and add it to the list of copy operations
            foreach (string directory in Files)
            {
                string destinationSubDirectory = Path.Combine(To, Path.GetFileName(directory));
                copyOperations.Add((directory, destinationSubDirectory));
                TotalFileCount += Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
            }
            
            // Use Parallel.For to copy each directory in parallel
            Parallel.For(0, copyOperations.Count, new ParallelOptions { MaxDegreeOfParallelism = numThreads }, i =>
            {
                // Call CopyDirectory with the progress indicator and reset event for the current thread
                CopyDirectory(copyOperations[i].source, copyOperations[i].destination, Progresses[AllThreads - numThreads + i].Progress, Progresses[AllThreads - numThreads + i].resetEvent, AllThreads - numThreads + i);
               
            });
        }
        private void CopyDirectory(string sourceDirectory, string destinationDirectory, IProgress<double> progress, ManualResetEventSlim resetEvent, int idx)
        {
            // Get the files in the source directory
            string[] files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);

            // Get the total number of files to copy
            int totalFiles = files.Length;

            // Initialize a counter for the number of files copied by this thread
            int filesCopied = 0;

            // Iterate over each file and copy it to the destination director
            foreach (string file in files)
            {
                // Wait for the reset event to be signaled
                resetEvent.Wait();
                Progresses[idx].CurrentFile = file;
                OnPropertyChanged(nameof(ProgressViews));
                // Copy the file to the destination directory
                string relativePath = file.Substring(sourceDirectory.Length + 1);
                string destinationFile = Path.Combine(destinationDirectory, relativePath);
                if (!Directory.Exists(destinationFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                }
                File.Copy(file, destinationFile, true);

                // Increment the files copied counter using Interlocked
                Interlocked.Increment(ref filesCopied);
                CopiedFileCount++;
                OnPropertyChanged(nameof(Progressstr));
                // Calculate the progress for this thread and report it using the IProgress object
                double threadProgress = (double)filesCopied / totalFiles * 100;
                Progresses[idx].ProgressValue = threadProgress;
                progress.Report(threadProgress);
                OnPropertyChanged(nameof(ProgressViews));
                OnPropertyChanged(nameof(ProgressesAvg));
                if (Progresses[idx].State == CopyState.Canceled)
                {
                    break;
                }
            }
            Progresses[idx].Status = "Finsihed";
            if (Progresses.All(x => x.ProgressValue > 99 || x.State == CopyState.Canceled || x.Status == "Finished"))
            {
                Progresses.Clear();
                AllThreads = 0;
                MessageBox.Show("Success!");
                TotalFileCount = 0;
                CopiedFileCount = 0;
                Files.Clear();
                To = "";
                OnPropertyChanged(nameof(Files));
                OnPropertyChanged(nameof(To));
                OnPropertyChanged(nameof(ProgressViews));
                OnPropertyChanged(nameof(ProgressesAvg));
                OnPropertyChanged(nameof(Progressstr));
            }
        }
        //Commands
        public ICommand GetFilesToCopy => new RelayCommand(x =>
        {
            FolderBrowserDialog folderBrowserDialog = new()
            {
                Title = "Select Directories",
                InitialFolder = @"C:\",
                AllowMultiSelect = true
            };
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Files = new(folderBrowserDialog.SelectedFolders);
                OnPropertyChanged(nameof(Files));

            }
        });
        public ICommand GetDestination => new RelayCommand(x =>
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Title = "Select Destination";
            folderBrowserDialog.InitialFolder = @"C:";
            folderBrowserDialog.AllowMultiSelect = false;

            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                To = folderBrowserDialog.SelectedFolders.First();
                OnPropertyChanged(nameof(To));
            }
        });
        public ICommand Copy => new RelayCommand(async x =>
        {
            await Task.Run(() =>
            {
                CopyDirectories();
            });

        });
        //Pause/cancel
        public ICommand PauseOrResumeAll => new RelayCommand(x =>
        {
            if (GeneralState == CopyState.Working)
            {
                Progresses.ForEach(x => { x.resetEvent.Reset(); x.State = CopyState.Paused; });
                GeneralState = CopyState.Paused;
            }
            else
            {
                Progresses.ForEach(x => { x.resetEvent.Set(); x.State = CopyState.Working; });
                GeneralState = CopyState.Working;
            }
            OnPropertyChanged(nameof(Progresses));
            OnPropertyChanged(nameof(GeneralState));
            OnPropertyChanged(nameof(ButtonBrush));
            OnPropertyChanged(nameof(BarContent));
        });
        public ICommand CalcelAll => new RelayCommand(x =>
        {
            Thread.Sleep(1000);
            Progresses.ForEach(x => x.State = CopyState.Canceled);

        });

    }
}
