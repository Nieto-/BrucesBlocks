using System;
using System.Windows;

namespace BrucesBlocks
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public void SetSkin(string name)
        {
            ResourceDictionary dictionaryColors = LoadComponent(new Uri(string.Format("/Styles/{0}.xaml", name), UriKind.Relative)) as ResourceDictionary;
            ResourceDictionary dictionaryStyles = LoadComponent(new Uri("/Styles/Common.xaml", UriKind.Relative)) as ResourceDictionary;
            if (dictionaryColors != null && dictionaryStyles != null)
            {
                Resources.BeginInit();
                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(dictionaryColors);
                Resources.MergedDictionaries.Add(dictionaryStyles);
                Resources.EndInit();
            }
        }
    }
}
