using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;


class CalcHeight
{
    public double Calc(StackPanel grid)
    {
        double totalHeight = 0;

        foreach (UIElement child in grid.Children)
        {
            if (child is FrameworkElement frameworkElement)
            {
                if (frameworkElement.Visibility != Visibility.Visible)
                {
                    continue;
                }

                frameworkElement.UpdateLayout();
                frameworkElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                if (frameworkElement.Name == "BlockHistoryGrid" || frameworkElement.Name == "BlockHistoryCard")
                {
                    totalHeight += 161 +
                                   frameworkElement.Margin.Top +
                                   frameworkElement.Margin.Bottom;
                }
                else
                {
                    totalHeight += frameworkElement.DesiredSize.Height;
                }
            }
        }
        return totalHeight + 32;
    }
}

