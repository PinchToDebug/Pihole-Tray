using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;


public class DnsRoutesLoader
{
    bool isAnimating = false;
    public async Task<Dictionary<string, string>> GetData(dynamic json)
    {
        var dict = new Dictionary<string, string>(); // wip
        await Task.Run(() =>
        {
            foreach (var item in json)
            {
                string key = item["name"]!.ToString();
                string value = item["count"]!.ToString();
                dict[key] = value;
            }
        });
        return dict;
    }

    public async Task LoadAsync(Grid grid, dynamic json, bool isV6)
    {
        if (json == null)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.Children.Add(new TextBlock { Text = "Object is null" });
            return;
        }
        BrushConverter brushConverter = new BrushConverter();
        var newData = new Dictionary<string, string>();
        var existingRows = new Dictionary<int, (string type, string percentage)>();
        var elementsToUpdate = new Dictionary<int, string>();

        try
        {
            if (isV6)
            {

                await Task.Run(() =>
                {
                    int count = 0;
                    foreach (var item in (JArray)json)
                    {
                        count += (int)item["count"]!;
                    }
                    foreach (var item in (JArray)json)
                    {
                        string nameOfUpStreamSource ="";
                       
                        if (string.IsNullOrEmpty(item["name"].ToString()))
                        {
                            nameOfUpStreamSource = item["ip"].ToString();

                        }
                        else if(item["ip"].Contains("blocklist") || item["ip"].Contains("cached"))
                        {
                            nameOfUpStreamSource = $"{item["name"]} | {item["ip"]}";
                        }
                        else
                        {
                            nameOfUpStreamSource = item["name"].ToString();
                        }
                        double value = ((double)item["count"] / count) * 100;
                        var percentage = value.ToString("F2", CultureInfo.InvariantCulture);
                        if (percentage != "0.00") // If 0 don't add
                        {
                            newData[nameOfUpStreamSource] = percentage;
                        }
                    }

                });
            }

            else
            {
                await Task.Run(() =>
                {
                    foreach (var item in (JObject)json)
                    {
                        var typeOfDestination = item.Key.Split(':')[0].Replace("|", " | ");
                        if (double.TryParse(item.Value.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var percentageValue))
                        {
                            var percentage = percentageValue.ToString("F2", CultureInfo.InvariantCulture);
                            if (percentage != "0.00") // if 0 don't add
                            {
                                newData[typeOfDestination] = percentage;
                            }
                        }
                    }
                });
            }


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
                    ApplyAnimation(percentageBlock: percentageBlock, newPercentage);
                }
            }
            var rowsToAdd = newData.Where(pair => !existingRows.Values.Any(row => row.type == pair.Key)).ToList();
            
            // Adding new rows
            foreach (var (nameOfUpStreamSource, percentage) in rowsToAdd)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

                var nameOfUpStreamSourceBlock = new TextBlock
                {
                    Text = nameOfUpStreamSource,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };

                var percentageBlock = new TextBlock
                {
                    Text = percentage,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)brushConverter.ConvertFrom("#FFBBC4F7")
                };


                ApplyAnimation(percentageBlock: percentageBlock, percentage);


                Grid.SetColumn(nameOfUpStreamSourceBlock, 0);
                Grid.SetRow(nameOfUpStreamSourceBlock, grid.RowDefinitions.Count - 1);

                Grid.SetColumn(percentageBlock, 1);
                Grid.SetRow(percentageBlock, grid.RowDefinitions.Count - 1);

                grid.Children.Add(nameOfUpStreamSourceBlock);
                grid.Children.Add(percentageBlock);
            }

            // Remove rows that are no longer in newData
            var rowsToRemove = existingRows
                .Where(row => !newData.ContainsKey(row.Value.type))
                .Select(row => row.Key)
                .ToList();

            foreach (var rowIndex in rowsToRemove.OrderByDescending(index => index))
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                grid.RowDefinitions.Remove(rowDefinition);

                var elementsToRemove = grid.Children
                    .OfType<UIElement>()
                    .Where(child => Grid.GetRow(child) == rowIndex)
                    .ToList();

                foreach (var element in elementsToRemove)
                {
                    grid.Children.Remove(element);
                }
            }
        }
        catch (Exception e)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.Children.Add(new TextBlock { Text = e.Message });
            return;
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



        void ApplyAnimation(TextBlock percentageBlock, string percentage)
        {
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

                var slideBack = new DoubleAnimation(-widthDifference, 0, TimeSpan.FromMilliseconds(0));
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

                var originalText = percentage.Replace(" %",""); // Sometimes it gets stuck this helps with that
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
        }
    }
}

