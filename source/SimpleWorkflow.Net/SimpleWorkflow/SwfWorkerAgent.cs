using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace SimpleWorkflow
{
    public sealed partial class SwfWorkerAgent
    {
        public static IServiceProvider ServiceProvider { get; set; }

        public static ISwfWorkerAgent CreateActivityWorker<TActivityWorker>()
            where TActivityWorker : SwfActivityWorker
        {
            ValidateDependencies();
            return new StandardActivityAgent<TActivityWorker>(ServiceProvider);
        }

        public static ISwfWorkerAgent CreateDeciderWorker<TDeciderWorker>()
            where TDeciderWorker : SwfDeciderWorker
        {
            ValidateDependencies();
            return new StandardDeciderAgent<TDeciderWorker>(ServiceProvider);
        }

        private static void ValidateDependencies()
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("A ServiceProvider must be specified before SwfWorkerAgents can be created");
            }
        }
    }
}
