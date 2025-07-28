using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using WinRT.Interop;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        Microsoft.UI.Composition.Compositor _compositor;
        int Selected = 0;
        System.Timers.Timer _timer = new();
        Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
        int _SelectMargin;
        public MainWindow()
        {
            InitializeComponent();
            _compositor = Microsoft.UI.Xaml.Media.CompositionTarget.GetCompositorForCurrentThread();
            this.ExtendsContentIntoTitleBar = true;

            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _timer.Interval = 1000;
            _timer.Elapsed += timer_Elapsed;
            _timer.Start();
        }
        private async void OnLoad(object sender, RoutedEventArgs e)
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Get display information for the primary monitor
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            // Set window size to 90% of the work area
            int width = (int)(workArea.Width * 0.8);
            int height = (int)(workArea.Height * 0.8);

            _SelectMargin = -(width / 420) / 2;

            // Ensure minimum size
            width = Math.Max(width, 1024);
            height = Math.Max(height, 768);

            appWindow.Resize(new SizeInt32(width, height));

            StorageFolder appInstalledFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var targetFolder = await appInstalledFolder.GetFolderAsync(@"Assets\Games");
            var subfolders = await targetFolder.GetFoldersAsync();
            ImageStack.Children.Clear();
            for (int i = 0; i < subfolders.Count; i++)
            {
                StorageFile file;
                if (await subfolders[i].TryGetItemAsync("GameIcon.png") is StorageFile icon) file = icon;
                else file = await targetFolder.GetFileAsync("DefaultIcon.png");
                var stream = await file.OpenAsync(FileAccessMode.Read);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                var image = new Image
                {
                    Source = bitmap,
                    Height = 210,
                    Margin = new Thickness(5),
                    Tag = subfolders[i]
                };
                image.Tapped += Image_Tapped;

                image.PointerEntered += (s, e) =>
                {
                    SelectGame(ImageStack.Children.IndexOf(s as UIElement), Selected, true);
                    Selected = ImageStack.Children.IndexOf(s as UIElement);
                };
                ImageStack.Children.Add(image);
            }
            var anim = _compositor.CreateExpressionAnimation();
            anim.Expression = "(left.Scale.X - 1) * left.ActualSize.X + left.Translation.X";
            anim.Target = "Translation.X";
            var elements = ImageStack.Children;
            for (int j = 0; j < elements.Count - 1; j++)
            {
                anim.SetExpressionReferenceParameter("left", elements[j]);
                elements[j + 1].StartAnimation(anim);
            }
            SelectGame(Selected, Selected, false);
        }

        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Image? image = sender as Image;
            if (image != null)
            {
                RunGame((StorageFolder)image.Tag);
            }
        }
        private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch(e.Key)
            {
                case VirtualKey.Left:
                    if (Selected > 0)
                    {
                        Selected--;
                        SelectGame(Selected, Selected + 1, false);
                    }
                    break;
                    case VirtualKey.Right:
                    if (Selected < ImageStack.Children.Count - 1)
                    {
                        Selected++;
                        SelectGame(Selected, Selected - 1, false);
                    }
                    break;
                case VirtualKey.Enter:
                    RunGame(((Image)ImageStack.Children[Selected]).Tag as StorageFolder);
                    break;
            }
        }

        private void timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                Clock.Text = DateTime.Now.ToString("HH:mm");
            });
        }
        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
            if (delta > 0 && Selected > 0)
            {
                Selected--;
                SelectGame(Selected, Selected + 1, false);
            }
            else if (delta < 0 && Selected < ImageStack.Children.Count - 1)
            {
                Selected++;
                SelectGame(Selected, Selected - 1, false);
            }
        }
        private void SelectGame(int index, int before, bool pointer)
        {
            if ((ImageStack.Children[index] is Image img && img.Tag is StorageFolder tag))
            { 
                Title.Text = tag.Name; 
                if (File.Exists(tag.Path + @"\Description.txt"))
                {
                    var descriptionFile = tag.GetFileAsync("Description.txt").AsTask().Result;
                    if (descriptionFile != null)
                    {
                        using (var stream = descriptionFile.OpenStreamForReadAsync().Result)
                        using (var reader = new StreamReader(stream))
                        {
                            Subtitle.Text = reader.ReadToEnd();
                        }
                    }
                }
                else
                {
                    Subtitle.Text = "";
                }
            }
            ApplySpringAnimation(ImageStack.Children[index], 1.2f);
            
            if (!pointer) Scroller.ChangeView((Selected + _SelectMargin) * 222, null, null);
            if (before != index) ApplySpringAnimation(ImageStack.Children[before], 1.0f);
        }
        private async void RunGame(StorageFolder folder)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.CreateNoWindow = true;
            psi.FileName = (await folder.GetFileAsync("RunGame.exe")).Path;
            psi.WorkingDirectory = folder.Path;
            Process.Start(psi);
        }
        private void ApplySpringAnimation(UIElement element, float scale)
        {
            var animation = _compositor.CreateSpringVector3Animation();
            animation.Target = "Scale";
            animation.FinalValue = new Vector3(scale);
            animation.DampingRatio = 0.9f;
            animation.Period = TimeSpan.FromMilliseconds(50);

            element.CenterPoint = new Vector3(0f,element.ActualSize.Y,0f);
            element.StartAnimation(animation);
        }
    }
}
