using System.Windows.Controls;
using FanucRobotServerSH.ViewModels;

namespace FanucRobotServerSH.Views;

/// <summary>注释编辑与列表页。</summary>
public partial class CommentView : UserControl
{
    public CommentView()
    {
        InitializeComponent();
    }

    public void SetViewModel(CommentViewModel vm) => DataContext = vm;
}
