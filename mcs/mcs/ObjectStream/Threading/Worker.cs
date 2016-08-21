using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectStream.Threading
{
    internal class Worker
    {

        private readonly TaskScheduler _callbackThread;

        private static TaskScheduler CurrentTaskScheduler
        {
            get
            {
                return (SynchronizationContext.Current != null
                            ? TaskScheduler.FromCurrentSynchronizationContext()
                            : TaskScheduler.Default);
            }
        }

        public event WorkerExceptionEventHandler Error;

        public Worker()
            : this(CurrentTaskScheduler)
        {
        }

        public Worker(TaskScheduler callbackThread)
        {
            _callbackThread = callbackThread;
        }

        public void DoWork(Action action)
        {
            //new Thread(DoWorkImpl) { IsBackground = true }.Start(action);
            Task.Factory.StartNew(DoWorkImpl, action, CancellationToken.None, TaskCreationOptions.LongRunning, _callbackThread);
        }

        private void DoWorkImpl(object oAction)
        {
            var action = (Action) oAction;
            try
            {
                action();
            }
            catch (Exception e)
            {
                Callback(() => Fail(e));
            }
        }

        private void Fail(Exception exception)
        {
            if (Error != null)
                Error(exception);
        }

        private void Callback(Action action)
        {
            //new Thread(new ThreadStart(action)).Start();
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, _callbackThread);
        }
    }

    internal delegate void WorkerSucceededEventHandler();

    internal delegate void WorkerExceptionEventHandler(Exception exception);
}