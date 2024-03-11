using My.BaseViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace FilerWpf
{
    public enum CopyState
    {
        Working,Paused,Canceled
    }


    public class FileProgressBar
    {
        public FileProgressBar()
        {
            State = CopyState.Working;
            Status = "Working";
        }
        public CopyState State { get; set; }
        public IProgress<double> Progress { get; set; }
        public string CurrentFile { get; set; }
        public double ProgressValue { get; set; }
        public ManualResetEventSlim resetEvent { get; set; }
        public string Content { get => State == CopyState.Working ? "Pause" : "Resume"; }
        public string Status { get; set; } = "";
        public Brush ButtonColor { get => State == CopyState.Working ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Blue); }
        public ICommand PauseOrResumeThread => new RelayCommand(x =>
        {
            Thread.Sleep(100);
            if(State == CopyState.Working)
            {
                PauseThread();
            }
            else
            {
                ResumeThread();
            }
        });
        public void PauseThread()
        {
            State = CopyState.Paused;
            resetEvent.Reset();
            Status = "Paused";
        }
        public void ResumeThread()
        {
            State = CopyState.Working;
            resetEvent.Set();
            Status = "Working";
        }
        public ICommand CancelThread => new RelayCommand(x =>
        {     
            Thread.Sleep(100);
            State = CopyState.Canceled;
            resetEvent.Set();
            Status = "Cancelled, waiting for others to finish...";
        });
    }
}
