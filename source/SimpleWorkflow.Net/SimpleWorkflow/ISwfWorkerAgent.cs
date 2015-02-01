using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleWorkflow
{
    public interface ISwfWorkerAgent
    {
        void Start();
        void Stop();
    }
}
