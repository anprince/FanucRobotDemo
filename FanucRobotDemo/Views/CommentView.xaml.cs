using System.Windows.Controls;
using FanucRobotDemoSH.ViewModels;

namespace FanucRobotDemoSH.Views;

/// <summary>注释页。</summary>
public partial class CommentView : UserControl
{
    public CommentView()
    {
        InitializeComponent();
    }

    public void SetViewModel(CommentViewModel vm) => DataContext = vm;
}
