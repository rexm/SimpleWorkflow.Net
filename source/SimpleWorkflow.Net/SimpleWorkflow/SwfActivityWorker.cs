using Amazon.SimpleWorkflow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWorkflow
{
    public abstract class SwfActivityWorker : IDisposable
    {
        public abstract Task HandleActivityTask(ActivityTask activityTask);

        public virtual void Dispose()
        {
        }
    }
}
