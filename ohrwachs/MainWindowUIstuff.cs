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

