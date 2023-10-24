using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ohrwachs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        OhrwachsEmpfaenger ow = null;

        //*****************************************************************************************************************************************************
        public MainWindow()
        {
            InitializeComponent();
            AllocConsole();

            Closing += OnWindowClosing;

            ow = new OhrwachsEmpfaenger();
            Startthread();
        }

        //*****************************************************************************************************************************************************
        void Startthread() // https://www.youtube.com/watch?v=qeMFqkcPYcg
        {
            Thread thread = new Thread(new ThreadStart(ThreadJob));
            thread.Start();
        }

        //*****************************************************************************************************************************************************
        void ThreadJob() // https://www.youtube.com/watch?v=TzFnYcIqj6I
        {
            ow.StartClient();
        }

        //*****************************************************************************************************************************************************
        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            ow.die = true;
        }

    }
}
