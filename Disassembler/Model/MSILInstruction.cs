using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace Disassembler.Model
{
    public class MSILInstruction
    {
        public OpCode OpCode { get; set; }
        public Int64 MetadataToken { get; set; }
        public int Lenght { get; set; }
    }
}
