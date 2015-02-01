using Amazon.SimpleWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Amazon.SimpleWorkflow.Model;

namespace SimpleWorkflow
{
    public partial class SwfWorkerAgent
    {
        private class StandardDeciderAgent<TDeciderWorker> : ISwfWorkerAgent
            where TDeciderWorker : SwfDeciderWorker
        {
            private bool _isRunning = false;
            private readonly IAmazonSimpleWorkflow _workflow;
            private readonly IServiceProvider _serviceProvider;

            public StandardDeciderAgent(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
                _workflow = (IAmazonSimpleWorkflow)_serviceProvider.GetService(typeof(IAmazonSimpleWorkflow));
            }

            void ISwfWorkerAgent.Start()
            {
                _isRunning = true;
                var runWorker = RunDecider(typeof(TDeciderWorker));
                Task.WaitAny(runWorker);
            }

            void ISwfWorkerAgent.Stop()
            {
                _isRunning = false;
            }

            private async Task RunDecider(Type deciderType)
            {
                var domain = deciderType.GetCustomAttribute<SwfWorkerAttribute>().Domain;
                var taskList = deciderType.GetCustomAttribute<SwfWorkerAttribute>().TaskList;
                var workerId = Guid.NewGuid().ToString();
                while (_isRunning)
                {
                    var decisionTask = await GetDecisionTask(domain, taskList, workerId);
                    if (string.IsNullOrEmpty(decisionTask.TaskToken))
                    {
                        continue;
                    }
                    await ExecuteDecisionTaskHandler(deciderType, decisionTask);
                }
            }

            private async Task ExecuteDecisionTaskHandler(Type deciderType, DecisionTask decisionTask)
            {
                Exception exception = null;
                try
                {
                    using (var decider = (SwfDeciderWorker)_serviceProvider.GetService(deciderType))
                    {
                        var decisions = decider.HandleDecisionTask(decisionTask);
                        await _workflow.RespondDecisionTaskCompletedAsync(new RespondDecisionTaskCompletedRequest
                        {
                            TaskToken = decisionTask.TaskToken,
                            Decisions = decisions
                        });
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                if (exception != null)
                {
                    await _workflow.RespondDecisionTaskCompletedAsync(new RespondDecisionTaskCompletedRequest
                    {
                        TaskToken = decisionTask.TaskToken,
                        Decisions = new List<Decision>
                    {
                        new Decision
                        {
                            DecisionType = new DecisionType("FailWorkflowExecution"),
                            FailWorkflowExecutionDecisionAttributes = new FailWorkflowExecutionDecisionAttributes
                            {
                                Reason = "exception",
                                Details = string.Format("{0}\n\n{1}",
                                    exception.Message,
                                    exception.StackTrace)
                            }
                        }
                    }
                    });
                }
            }

            private async Task<DecisionTask> GetDecisionTask(string domain, string taskList, string workerId)
            {
                PollForDecisionTaskResponse response = null;
                do
                {
                    response = await PollForDecisionTaskAsync(domain, taskList, workerId);
                }
                while (_isRunning && string.IsNullOrEmpty(response.DecisionTask.TaskToken));
                await AccumulateAdditionalPages(domain, taskList, workerId, response.DecisionTask);
                return response.DecisionTask;
            }

            private async Task AccumulateAdditionalPages(string domain, string taskList, string workerId, DecisionTask decisionTask)
            {
                var nextPageToken = decisionTask.NextPageToken;
                while (string.IsNullOrEmpty(nextPageToken) == false)
                {
                    var additionalPage = await PollForDecisionTaskAsync(domain, taskList, workerId, nextPageToken);
                    nextPageToken = additionalPage.DecisionTask.NextPageToken;
                    decisionTask.Events = decisionTask.Events.Concat(additionalPage.DecisionTask.Events).ToList();
                }
            }

            private async Task<PollForDecisionTaskResponse> PollForDecisionTaskAsync(string domain, string taskList, string workerId, string nextPageToken = null)
            {
                return await _workflow.PollForDecisionTaskAsync(new PollForDecisionTaskRequest
                {
                    Domain = domain,
                    Identity = workerId,
                    TaskList = new TaskList
                    {
                        Name = taskList
                    },
                    NextPageToken = nextPageToken
                });
            }
        }
    }
}
