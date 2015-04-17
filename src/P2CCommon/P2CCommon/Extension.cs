using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace P2CCommon 
{
	public static class Extension
	{

		// Allow non-ui thread to execute instruction on the ui thread
		public static void InvokeIfRequired(this Control control, Action action)
		{
			if(control.Dispatcher.CheckAccess())
			{
				action();
			}
			else
			{
				control.Dispatcher.Invoke(action);
			}
		}
	}
}
