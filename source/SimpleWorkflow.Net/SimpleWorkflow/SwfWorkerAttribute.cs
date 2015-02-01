using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWorkflow
{
    public sealed class SwfWorkerAttribute : Attribute
    {
        public string Domain { get; set; }

        public string TaskList { get; set; }

        public string Version { get; set; }
    }
}
