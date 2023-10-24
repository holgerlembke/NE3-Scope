using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ohrwachs
{
    public partial class MainWindow : Window
    {
        //*****************************************************************************************************************************************************
        private void setUIscale(double uiscale)
        {
            uiScaler.ScaleX = uiscale;
            uiScaler.ScaleY = uiscale;
        }

        //*****************************************************************************************************************************************************
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs args)
        {
            base.OnPreviewMouseWheel(args);

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                setUIscale(uiScaler.ScaleX + ((args.Delta > 0) ? 0.1 : -0.1));
            }
        }

    }
}

