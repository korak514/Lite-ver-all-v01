using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class NewRowData
    {
        // Key: ColumnName, Value: Entered Value
        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
    }
}