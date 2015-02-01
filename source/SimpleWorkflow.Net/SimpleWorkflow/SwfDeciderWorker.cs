using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWorkflow
{
    public abstract class SwfDeciderWorker : IDisposable
    {
        private List<Decision> _decisions;

        protected void ScheduleActivity(ScheduleActivityTaskDecisionAttributes activityDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("ScheduleActivityTask"),
                ScheduleActivityTaskDecisionAttributes = activityDecisionInfo
            });
        }

        protected void RequestCancelActivity(RequestCancelActivityTaskDecisionAttributes requestCancelActivity)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("RequestCancelActivity"),
                RequestCancelActivityTaskDecisionAttributes = requestCancelActivity
            });
        }

        protected void CompleteWorkflow(CompleteWorkflowExecutionDecisionAttributes completeWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("CompleteWorkflowExecution"),
                CompleteWorkflowExecutionDecisionAttributes = completeWorkflowDecisionInfo
            });
        }

        protected void CompleteWorkflow(string result)
        {
            CompleteWorkflow(new CompleteWorkflowExecutionDecisionAttributes
            {
                Result = result
            });
        }

        protected void FailWorkflow(FailWorkflowExecutionDecisionAttributes failWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("FailWorkflowExecution"),
                FailWorkflowExecutionDecisionAttributes = failWorkflowDecisionInfo
            });
        }

        protected void FailWorkflow(string reason, string details)
        {
            FailWorkflow(new FailWorkflowExecutionDecisionAttributes
            {
                Reason = reason,
                Details = details
            });
        }

        protected void CancelWorkflowExecution(CancelWorkflowExecutionDecisionAttributes cancelWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("CancelWorkflowExecution"),
                CancelWorkflowExecutionDecisionAttributes = cancelWorkflowDecisionInfo
            });
        }

        protected void StartChildWorkflow(StartChildWorkflowExecutionDecisionAttributes startChildWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("StartChildWorkflowExecution"),
                StartChildWorkflowExecutionDecisionAttributes = startChildWorkflowDecisionInfo
            });
        }

        protected void CancelChildWorkflow(CancelWorkflowExecutionDecisionAttributes cancelWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("CancelWorkflowExecution"),
                CancelWorkflowExecutionDecisionAttributes = cancelWorkflowDecisionInfo
            });
        }

        protected void RequestCancelExternalWorkflowExecution(RequestCancelExternalWorkflowExecutionDecisionAttributes requestCancelExternalWorkflowDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("RequestCancelExternalWorkflowExecution"),
                RequestCancelExternalWorkflowExecutionDecisionAttributes = requestCancelExternalWorkflowDecisionInfo
            });
        }

        protected void RecordMarker(RecordMarkerDecisionAttributes recordMarkerDecisionInfo)
        {
            _decisions.Add(new Decision
            {
                DecisionType = new DecisionType("RecordMarker"),
                RecordMarkerDecisionAttributes = recordMarkerDecisionInfo
            });
        }

        protected ActivityTaskScheduledEventAttributes GetScheduleActivityEvent(
            ActivityTaskCompletedEventAttributes activityTaskCompleted, DecisionTask decisionTask)
        {
            return decisionTask.Events.FirstOrDefault(e => e.EventId == activityTaskCompleted.ScheduledEventId).ActivityTaskScheduledEventAttributes;
        }

        protected ActivityTaskScheduledEventAttributes GetScheduleActivityEvent(
            ActivityTaskFailedEventAttributes activityTaskFailed, DecisionTask decisionTask)
        {
            return decisionTask.Events.FirstOrDefault(e => e.EventId == activityTaskFailed.ScheduledEventId).ActivityTaskScheduledEventAttributes;
        }

        protected ActivityTaskScheduledEventAttributes GetScheduleActivityEvent(
            ActivityTaskTimedOutEventAttributes activityTaskTimedOut, DecisionTask decisionTask)
        {
            return decisionTask.Events.FirstOrDefault(e => e.EventId == activityTaskTimedOut.ScheduledEventId).ActivityTaskScheduledEventAttributes;
        }

        protected abstract void WorkflowExecutionStarted(
            WorkflowExecutionStartedEventAttributes attributes, DecisionTask decisionTask);

        protected virtual void ActivityTaskCompleted(
            ActivityTaskCompletedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ActivityTaskFailed(
            ActivityTaskFailedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ActivityTaskTimedOut(
            ActivityTaskTimedOutEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ActivityTaskCanceled(
            ActivityTaskCanceledEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ChildWorkflowExecutionCompleted(
            ChildWorkflowExecutionCompletedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ChildWorkflowExecutionFailed(
            ChildWorkflowExecutionFailedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ChildWorkflowExecutionCanceled(
            ChildWorkflowExecutionCanceledEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ChildWorkflowExecutionTimedOut(
            ChildWorkflowExecutionTimedOutEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void ChildWorkflowExecutionTerminated(
            ChildWorkflowExecutionTerminatedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void WorkflowExecutionCancelRequested(
            WorkflowExecutionCancelRequestedEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        protected virtual void WorkflowExecutionSignaled(
            WorkflowExecutionSignaledEventAttributes attributes, DecisionTask decisionTask)
        {
        }

        public List<Decision> HandleDecisionTask(DecisionTask decisionTask)
        {
            _decisions = new List<Decision>();
            foreach (var historyEvent in decisionTask.Events)
            {
                if (historyEvent.EventId <= decisionTask.PreviousStartedEventId)
                {
                    continue;
                }
                else
                {
                    var handlerMethodForEventType = this.GetType().GetMethod(
                        historyEvent.EventType, BindingFlags.NonPublic | BindingFlags.Instance);
                    var eventAttributesProperty = historyEvent.GetType().GetProperty(historyEvent.EventType + "EventAttributes");
                    if (handlerMethodForEventType != null && eventAttributesProperty != null)
                    {
                        try
                        {
                            handlerMethodForEventType.Invoke(this, new object[]
                            {
                                eventAttributesProperty.GetValue(historyEvent),
                                decisionTask
                            });
                        }
                        catch (Exception ex)
                        {
                            ex = ex.InnerException;
                            _decisions.Clear();
                            FailWorkflow(new FailWorkflowExecutionDecisionAttributes
                            {
                                Reason = "exception",
                                Details = string.Format("Unhandled exception in {0}: {1}\n\n{2}",
                                    handlerMethodForEventType.Name,
                                    ex.Message,
                                    ex.StackTrace)
                            });
                            break;
                        }
                    }
                }
            }
            return _decisions;
        }

        public virtual void Dispose()
        {
        }
    }
}
