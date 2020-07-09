using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace paxos
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Paxos paxos;
        public MainWindow()
        {
            InitializeComponent();
            paxos = new Paxos();
            paxos.init(MainCanvas,txtStatus);
            //Render.circle(10, 10, 100, 100, MainCanvas);

            //Ellipse ell = new Ellipse();
            //ell.Height = 30;
            //ell.Width = 30;
            ////ell.Fill =
            //Canvas myCanvas = new Canvas();
            //myCanvas.MouseDown += MouseMove;
            //myCanvas.Background = Brushes.LightSteelBlue;
            //Canvas.SetTop(ell, 100);
            //Canvas.SetLeft(ell, 10);
            //myCanvas.Children.Add(ell);

            //this.Content=myCanvas;
        }
        bool MouseDownOdd = false;
        private void MouseDown(object sender, MouseEventArgs e)
        {
            //foreach(UIElement it in MainCanvas.Children)
            //{
            //    if(it is Ellipse)
            //    {
            //        var cur = (it as Ellipse);
            //        cur.Stroke = Brushes.Blue;
            //        cur.SetValue(Canvas.LeftProperty, (double)cur.GetValue(Canvas.LeftProperty)+10);
            //        cur.SetValue(Canvas.TopProperty, (double)cur.GetValue(Canvas.TopProperty)+10);
            //    }
            //}
            MouseDownOdd = !MouseDownOdd;
            if (MouseDownOdd)
            {
                paxos.stop();
            }else
            {
                paxos.resume();
            }
        }

    }

    //public partial class SetBackgroundColorOfShapeExample : Page
    //{
    //    public SetBackgroundColorOfShapeExample()
    //    {
    //        // Create a StackPanel to contain the shape.
    //        StackPanel myStackPanel = new StackPanel();

    //        // Create a red Ellipse.
    //        Ellipse myEllipse = new Ellipse();

    //        // Create a SolidColorBrush with a red color to fill the 
    //        // Ellipse with.
    //        SolidColorBrush mySolidColorBrush = new SolidColorBrush();

    //        // Describes the brush's color using RGB values. 
    //        // Each value has a range of 0-255.
    //        mySolidColorBrush.Color = Color.FromArgb(255, 255, 255, 0);
    //        myEllipse.Fill = mySolidColorBrush;
    //        myEllipse.StrokeThickness = 2;
    //        myEllipse.Stroke = Brushes.Black;

    //        // Set the width and height of the Ellipse.
    //        myEllipse.Width = 200;
    //        myEllipse.Height = 100;

    //        // Add the Ellipse to the StackPanel.
    //        myStackPanel.Children.Add(myEllipse);

    //        this.Content = myStackPanel;
    //    }

    //}

}
