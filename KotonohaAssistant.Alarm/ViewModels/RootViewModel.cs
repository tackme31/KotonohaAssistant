using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class RootViewModel : ObservableObject
{
    public RootViewModel()
    {
    }

    [ObservableProperty]
    private bool _isApplicationLoaded;

    internal void OnApplicationLoaded()
    {
        if (IsApplicationLoaded)
        {
            return;
        }

        IsApplicationLoaded = true;
    }
}
