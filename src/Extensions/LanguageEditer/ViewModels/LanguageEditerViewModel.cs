using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.Pickers;
using LanguageEditer.Model;
using Microsoft.UI.Xaml.Controls;
using Org.BouncyCastle.Crypto;
using Syncfusion.UI.Xaml.DataGrid;
using WinRT.Interop;

namespace LanguageEditer.ViewModels;

public partial class LanguageEditerViewModel : ObservableObject
{
    private SfDataGrid _dataGrid;
    private readonly NativePickersService _pickers = new(() => WindowNative.GetWindowHandle(App.Window));

    [RelayCommand]
    void CreateNewProject() { }

    [ObservableProperty]
    public partial ObservableCollection<ProjectLanguageModel> ProjectLanguages { get; set; } = [];

    [ObservableProperty]
    public partial string QueryText { get; set; }

    [RelayCommand]
    async Task OpenDocument()
    {
        var file = await _pickers.GetFileOpenPicker([".json"]);
        if (file is null)
            return;
        if (File.Exists(file.Path))
        {
            ObservableCollection<ProjectLanguageModel> models = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(file.Path),
                ProjectLanguageModelContext.Default.ObservableCollectionProjectLanguageModel
            )!;
            this.ProjectLanguages = models;
        }
    }

    [RelayCommand]
    async Task SaveDocument()
    {
        var file = await _pickers.GetFileSavePicker([".json"], "NewDocument");
        if (file == null)
            return;
        if (File.Exists(file.Path))
        {
            File.Delete(file.Path);
        }
        var json = JsonSerializer.Serialize(
            ProjectLanguages,
            ProjectLanguageModelContext.Default.ObservableCollectionProjectLanguageModel
        );
        var stream = File.CreateText(file.Path);
        await stream.WriteAsync(json);
        await stream.FlushAsync();
        stream.Close();
        await stream.DisposeAsync();
    }

    [RelayCommand]
    void Loaded()
    {
        this._dataGrid.View.Filter = FilterRecords;
        this._dataGrid.View.RefreshFilter();
    }

    [RelayCommand]
    async Task GeneratedLanguage()
    {
        try
        {
            var result = await _pickers.GetFolderPicker();
            if (result is null)
                return;
            if (Directory.GetFiles(result.Path, "*", SearchOption.AllDirectories).Count() > 0)
            {
                MessageBox(IntPtr.Zero, $"选择的必须是一个空文件夹！", "警告", 0);
            }
            Dictionary<string, List<LanguageItem>> items =
                new Dictionary<string, List<LanguageItem>>();
            var list = this
                .ProjectLanguages.GroupBy(x => x.Keys)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();
            #region Chinese
            if (list != null && list.Any())
            {
                MessageBox(IntPtr.Zero, $"数据有重合项！Key：" + string.Join(",", list), "警告", 0);
                return;
            }
            #region 数据生成

            List<LanguageItem> JT = [];
            List<LanguageItem> FT = [];
            List<LanguageItem> EN = [];
            List<LanguageItem> JP = [];
            foreach (var item in this.ProjectLanguages)
            {
                JT.Add(new() { Key = item.Keys, Value = item.ZH_Hans });
            }
            foreach (var item in this.ProjectLanguages)
            {
                FT.Add(new() { Key = item.Keys, Value = item.ZH_Hant });
            }
            foreach (var item in this.ProjectLanguages)
            {
                JP.Add(new() { Key = item.Keys, Value = item.Ja_Jp });
            }
            foreach (var item in this.ProjectLanguages)
            {
                EN.Add(new() { Key = item.Keys, Value = item.EN_Us });
            }
            #endregion
            #region 语言Json
            var jtJson = JsonSerializer.Serialize(
                JT,
                ProjectLanguageModelContext.Default.ListLanguageItem
            );
            var ftJson = JsonSerializer.Serialize(
                FT,
                ProjectLanguageModelContext.Default.ListLanguageItem
            );
            var enJson = JsonSerializer.Serialize(
                EN,
                ProjectLanguageModelContext.Default.ListLanguageItem
            );
            var jpJson = JsonSerializer.Serialize(
                JP,
                ProjectLanguageModelContext.Default.ListLanguageItem
            );
            await File.WriteAllTextAsync(
                result.Path + "\\zh-Hans.json",
                jtJson,
                encoding: Encoding.UTF8
            );
            await File.WriteAllTextAsync(
                result.Path + "\\zh-Hant.json",
                ftJson,
                encoding: Encoding.UTF8
            );
            await File.WriteAllTextAsync(
                result.Path + "\\en-US.json",
                enJson,
                encoding: Encoding.UTF8
            );
            await File.WriteAllTextAsync(
                result.Path + "\\ja-JP.json",
                jpJson,
                encoding: Encoding.UTF8
            );
            #endregion
            #endregion
            MessageBox(IntPtr.Zero, $"生成合并成功！", "提示", 0);
        }
        catch (Exception ex) 
        {
            MessageBox(IntPtr.Zero, ex.Message, "警告", 0);
        }
    }

    internal void SetDataGrid(SfDataGrid dataGrid)
    {
        this._dataGrid = dataGrid;
    }

    [DllImport("user32.dll", EntryPoint = "MessageBox", CharSet = CharSet.Auto)]
    public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    public bool FilterRecords(object o)
    {
        var queryText = this.QueryText;
        var item = o as ProjectLanguageModel;
        if (item != null)
        {
            if (
                item.EN_Us.Contains(queryText)
                || item.Keys.Contains(queryText)
                || item.ZH_Hans.Contains(queryText)
                || item.ZH_Hant.Contains(queryText)
                || item.Ja_Jp.Contains(queryText)
            )
                return true;
        }
        return false;
    }
}
