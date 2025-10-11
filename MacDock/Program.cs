using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MacDock
{
    internal static class Program
    {
        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MacDockControl dock = new MacDockControl
            {
                //DockPosition = DockPosition.Bottom, // Cambia in Top, Left, Right
                //IconSize = 32
            };

            Application.Run(dock);
        }
    }
}
