using System.Windows.Controls;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Globalization;
using System.Diagnostics;
using System.Windows.Media.Effects;


public class QueryTypesLoader
{
    public async Task LoadAsync(Grid grid, JObject obj)
    {

        BrushConverter brushConverter = new BrushConverter();
        var newData = new Dictionary<string, string>();
        var existingRows = new Dictionary<int, (string type, string percentage)>();
        var elementsToUpdate = new Dictionary<int, string>();

        await Task.Run(() =>
        {
            foreach (var item in obj)
            {
                var typeOfRequest = item.Key;
              
                if (double.TryParse(item.Value!.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var percentageValue))
                {
                    var percentage = percentageValue.ToString("F2", CultureInfo.InvariantCulture);
                    if (percentage != "0.00") // if 0 don't add
                    {
                        newData[typeOfRequest] = percentage;
                    }
                }

            }
        });



        for (int i = 0; i < grid.RowDefinitions.Count; i++)
        {
            var typeBlock = grid.Children.OfType<TextBlock>()
                .FirstOrDefault(tb => Grid.GetRow(tb) == i && Grid.GetColumn(tb) == 0);
            var percentageBlock = grid.Children.OfType<TextBlock>()
                .FirstOrDefault(tb => Grid.GetRow(tb) == i && Grid.GetColumn(tb) == 1);

            if (typeBlock != null && percentageBlock != null)
            {
                string percentageText = percentageBlock.Text.EndsWith(" %")
                    ? percentageBlock.Text.Substring(0, percentageBlock.Text.Length - 2)
                    : percentageBlock.Text;

                existingRows[i] = (typeBlock.Text, percentageText);

                if (newData.TryGetValue(typeBlock.Text, out var newPercentage))
                {
                    if (newPercentage != percentageText)
                    {

                        if (percentageBlock.IsMouseOver)
                        {
                            elementsToUpdate[i] = $"{newPercentage} %";
                        }
                        else
                        {
                            elementsToUpdate[i] = newPercentage;

                        }
                        if (newPercentage != percentageText)
                        {
                            if (percentageBlock.IsMouseOver)
                            {
                                elementsToUpdate[i] = $"{newPercentage} %";
                            }
                            else
                            {
                                elementsToUpdate[i] = newPercentage;
                            }
                        }

                    }
                }
            }
        }


        foreach (var (rowIndex, newPercentage) in elementsToUpdate)
        {
            var percentageBlock = grid.Children.OfType<TextBlock>()
                .FirstOrDefault(tb => Grid.GetRow(tb) == rowIndex && Grid.GetColumn(tb) == 1);

            if (percentageBlock != null)
            {
                percentageBlock.Text = newPercentage;
            }
        }

        var rowsToAdd = newData
            .Where(pair => !existingRows.Values.Any(row => row.type == pair.Key))
            .ToList();

        // adding new rows
        foreach (var (typeOfRequest, percentage) in rowsToAdd)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

            var typeBlock = new TextBlock
            {
                Text = typeOfRequest,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var percentageBlock = new TextBlock
            {

                TextAlignment = TextAlignment.Right,
                Text = percentage,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = (Brush)brushConverter.ConvertFrom("#FFBBC4F7")
            };

            //percentageBlock.MouseEnter += (s, e) => percentageBlock.Text = $"{percentage} %";
            //percentageBlock.MouseLeave += (s, e) => percentageBlock.Text = percentage;



            bool isAnimating = false;
            var translateTransform = new TranslateTransform();
            percentageBlock.RenderTransform = translateTransform;

            percentageBlock.MouseEnter += (s, e) =>
            {
                if (isAnimating)
                {
                    translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                }

                isAnimating = true;

                var originalText = percentageBlock.Text;
                var originalWidth = MeasureTextWidth(originalText, percentageBlock);
                var newText = $"{percentage} %";
                var newWidth = MeasureTextWidth(newText, percentageBlock);
                percentageBlock.Width = newWidth;
                var widthDifference = newWidth - originalWidth;

                var slideBack = new DoubleAnimation(0, 0, TimeSpan.FromMilliseconds(100));
                percentageBlock.Text = newText;

                slideBack.Completed += (s2, e2) =>
                {
                    isAnimating = false;
                };
                translateTransform.BeginAnimation(TranslateTransform.XProperty, slideBack);
            };



            percentageBlock.MouseLeave += (s, e) =>
            {
                if (isAnimating)
                {

                    translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                }

                var originalText = percentage;
                var originalWidth = MeasureTextWidth(originalText, percentageBlock);
                var newText = $"{percentage} %";
                var newWidth = MeasureTextWidth(newText, percentageBlock);
                var widthDifference = newWidth - originalWidth;
                percentageBlock.Width = originalWidth;

                var slideBack = new DoubleAnimation(-widthDifference, 0, TimeSpan.FromMilliseconds(100));
                slideBack.Completed += (s2, e2) =>
                {
                    percentageBlock.Text = originalText;
                    isAnimating = false;

                };
                translateTransform.BeginAnimation(TranslateTransform.XProperty, slideBack);
            };


            Grid.SetColumn(typeBlock, 0);
            Grid.SetRow(typeBlock, grid.RowDefinitions.Count - 1);

            Grid.SetColumn(percentageBlock, 1);
            Grid.SetRow(percentageBlock, grid.RowDefinitions.Count - 1);

            grid.Children.Add(typeBlock);
            grid.Children.Add(percentageBlock);
        }

        // remove rows that are no longer in new data
        var rowsToRemove = existingRows
            .Where(row => !newData.ContainsKey(row.Value.type))
            .Select(row => row.Key)
            .ToList();

        foreach (var rowIndex in rowsToRemove.OrderByDescending(index => index))
        {
            // remove row definition
            var rowDefinition = grid.RowDefinitions[rowIndex];
            grid.RowDefinitions.Remove(rowDefinition);

            // remove elements in the row
            var elementsToRemove = grid.Children
                .OfType<UIElement>()
                .Where(child => Grid.GetRow(child) == rowIndex)
                .ToList();

            foreach (var element in elementsToRemove)
            {
                grid.Children.Remove(element);
            }
        }


        double MeasureTextWidth(string text, TextBlock textBlock)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1);

            return formattedText.WidthIncludingTrailingWhitespace;
        }




    }

}
