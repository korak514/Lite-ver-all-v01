## Context Guide: DatarepView

### Project Metadata

**Target Framework:** .NET Framework 4.8 (x86 / AnyCPU)

**Key NuGet Packages:**
| Package | Version | Purpose |
|---------|---------|---------|
| LiveCharts.Wpf | 0.9.7 | Charting |
| FontAwesome.WPF | 4.7.0 | Icons (fa: namespace) |
| FontAwesome.Sharp | 6.6.0 | Icons (sharp: namespace) |
| Extended.Wpf.Toolkit | 4.7.x | RangeSlider, etc. (xctk: namespace) |
| EPPlus | 7.7.3 | Excel import/export |
| ClosedXML | 0.105.0 | Excel operations |
| LinqForEEPlus | 1.0.1 | LINQ over EPPlus |
| Newtonsoft.Json | 13.0.3 | JSON serialization |
| Microsoft.Data.SqlClient | 6.0.1 | SQL Server connectivity |
| Npgsql | 4.1.12 | PostgreSQL connectivity |
| Costura.Fody | 6.0.0 | Assembly embedding |

---

### File: DatarepView.xaml

```xml
<UserControl x:Class="WPF_LoginForm.Views.DatarepView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:local="clr-namespace:WPF_LoginForm.Views"
             xmlns:converters="clr-namespace:WPF_LoginForm.Converters"
             xmlns:systemData="clr-namespace:System.Data;assembly=System.Data"
             xmlns:viewModels="clr-namespace:WPF_LoginForm.ViewModels"
             xmlns:fa="http://schemas.fontawesome.io/icons/"
             xmlns:xctk="http://schemas.xceed.com/wpf/xaml/toolkit"
             xmlns:p="clr-namespace:WPF_LoginForm.Properties"
             mc:Ignorable="d"
             d:DesignHeight="500" d:DesignWidth="800"
             d:DataContext="{d:DesignInstance Type=viewModels:DatarepViewModel, IsDesignTimeCreatable=False}">

    <UserControl.Resources>
        <converters:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
        <converters:BooleanInverterConverter x:Key="BooleanInverter" />
        <converters:IsRowEditableConverter x:Key="IsRowEditableConverter" />
        <converters:IsItemInCollectionConverter x:Key="IsItemInCollectionConverter" />
        <converters:IsNullOrEmptyConverter x:Key="IsNullOrEmptyConverter" />
        <converters:DateTimeCorrectionConverter x:Key="DateTimeCorrectionConverter" />

        <Style x:Key="ErrorMessageTextBoxStyle" TargetType="TextBox">
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="Red" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="VerticalAlignment" Value="Center" />
            <Setter Property="IsReadOnly" Value="True" />
            <Setter Property="TextWrapping" Value="NoWrap" />
            <Setter Property="AcceptsReturn" Value="False" />
        </Style>

        <Style x:Key="StandardToggleButtonStyle" TargetType="ToggleButton">
            <Setter Property="Height" Value="40" />
            <Setter Property="Background" Value="#2A2A5A" />
            <Setter Property="Foreground" Value="White" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="BorderBrush" Value="#40FFFFFF" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                        <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsChecked" Value="True">
                    <Setter Property="Background" Value="{DynamicResource color5}" />
                    <Setter Property="BorderBrush" Value="White" />
                </Trigger>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="{DynamicResource HoverLightBlueBrush}" />
                </Trigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <Grid Background="{StaticResource Brush2}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- 1. GLOBAL BAR: Table & Search -->
        <Border Grid.Row="0" Background="{StaticResource color1}" Padding="15,8" BorderBrush="{StaticResource panelActiveColor}" BorderThickness="0,0,0,1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <StackPanel Orientation="Horizontal" Grid.Column="0">
                    <fa:ImageAwesome Icon="Database" Width="16" Foreground="{StaticResource color6}" Margin="0,0,10,0" VerticalAlignment="Center"/>
                    <TextBlock Text="{x:Static p:Resources.Table}" Foreground="White" FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,10,0" FontSize="12" />
                    <ComboBox ItemsSource="{Binding TableNames}" SelectedItem="{Binding SelectedTable, Mode=TwoWay}" DisplayMemberPath="." MinWidth="180" Height="28" VerticalContentAlignment="Center" />
                    
                    <Button Command="{Binding DeleteTableCommand}" Visibility="{Binding IsAdminAndOnline, Converter={StaticResource BooleanToVisibilityConverter}}"
                            Background="Transparent" BorderThickness="0" Margin="10,0,0,0" ToolTip="{x:Static p:Resources.Tip_DeleteTable}">
                        <fa:ImageAwesome Icon="Trash" Width="14" Foreground="#FF4B4B" />
                    </Button>
                </StackPanel>

                <StackPanel Orientation="Horizontal" Grid.Column="2" HorizontalAlignment="Right">
                    <Border Background="#1AFFFFFF" CornerRadius="12" Padding="10,1" Margin="0,0,10,0" BorderBrush="{StaticResource titleColor3}" BorderThickness="1">
                        <StackPanel Orientation="Horizontal">
                            <fa:ImageAwesome Icon="Search" Width="12" Foreground="LightGray" Margin="5,0,8,0" VerticalAlignment="Center"/>
                            <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" 
                                     Background="Transparent" BorderThickness="0" Foreground="White" 
                                     MinWidth="180" VerticalAlignment="Center" FontSize="12" />
                        </StackPanel>
                    </Border>
                    <CheckBox Content="{x:Static p:Resources.Str_Global}" IsChecked="{Binding IsGlobalSearchActive}" Foreground="White" VerticalAlignment="Center" Margin="0,0,15,0" FontSize="11" />
                    <Button Command="{Binding ClearSearchCommand}" Background="Transparent" BorderThickness="0" ToolTip="{x:Static p:Resources.Tip_ClearSearch}">
                        <fa:ImageAwesome Icon="TimesCircle" Width="16" Foreground="LightGray" />
                    </Button>
                </StackPanel>

                <StackPanel Orientation="Horizontal" Grid.Column="3" Margin="20,0,0,0">
                    <Button Command="{Binding DecreaseFontSizeCommand}" Background="Transparent" BorderThickness="0" Margin="5,0">
                        <fa:ImageAwesome Icon="Minus" Width="10" Foreground="White" />
                    </Button>
                    <TextBlock Text="A" Foreground="White" FontSize="12" VerticalAlignment="Center" FontWeight="Bold"/>
                    <Button Command="{Binding IncreaseFontSizeCommand}" Background="Transparent" BorderThickness="0" Margin="5,0">
                        <fa:ImageAwesome Icon="Plus" Width="10" Foreground="White" />
                    </Button>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 2. ACTION BAR: Data Operations -->
        <Border Grid.Row="1" Background="{StaticResource primaryBackColor2Brush}" Padding="10,5" BorderBrush="{StaticResource panelActiveColor}" BorderThickness="0,0,0,1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <Button Command="{Binding AddNewRowCommand}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#2ECC71" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Plus" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Str_NewRow}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding EditSelectedRowsCommand}" CommandParameter="{Binding ElementName=DataDisplayGrid, Path=SelectedItems}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#3498DB" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Edit" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Edit}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding DeleteSelectedRowCommand}" CommandParameter="{Binding ElementName=DataDisplayGrid, Path=SelectedItems}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#E74C3C" Margin="0,0,10,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Trash" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Delete}" FontSize="11"/></StackPanel>
                    </Button>
                    <Separator Background="#40FFFFFF" Margin="0,4,10,4"/>
                    <Button Command="{Binding SaveChangesCommand}" IsEnabled="{Binding IsDirty}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="{StaticResource color5}" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Save" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Save}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding UndoChangesCommand}" Style="{StaticResource StandardButtonStyle}" Background="#CC9900" Margin="0,0,5,0" Padding="10,3" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Undo" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Undo}" Foreground="White" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding ShowFindReplaceCommand}" Style="{StaticResource StandardButtonStyle}" Background="#5D6D7E" Margin="5,0,0,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="SearchPlus" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Str_Find}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding ReloadDataCommand}" Style="{StaticResource StandardButtonStyle}" Background="#2A2A5A" Margin="5,0,0,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Refresh" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Reload}" FontSize="11"/></StackPanel>
                    </Button>
                </StackPanel>

                <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="10,0">
                    <TextBlock Text="{Binding ErrorMessage}" Foreground="#E74C3C" FontWeight="Bold" FontSize="11" Visibility="{Binding HasError, Converter={StaticResource BooleanToVisibilityConverter}}"/>
                </StackPanel>

                <StackPanel Grid.Column="2" Orientation="Horizontal">
                    <ToggleButton IsChecked="{Binding IsColumnSelectorVisible, Mode=TwoWay}" Style="{StaticResource StandardToggleButtonStyle}" Margin="0,0,5,0" Padding="10,3" Height="28">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Gear" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Str_Options}" FontSize="11"/></StackPanel>
                    </ToggleButton>
                    <ToggleButton IsChecked="{Binding IsDateFilterPanelVisible, Mode=TwoWay}" Style="{StaticResource StandardToggleButtonStyle}" Margin="0,0,5,0" Padding="10,3" Height="28" Visibility="{Binding IsDateFilterVisible, Converter={StaticResource BooleanToVisibilityConverter}}">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Calendar" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Filter}" FontSize="11"/></StackPanel>
                    </ToggleButton>
                    <Separator Background="#40FFFFFF" Margin="5,4,5,4"/>
                    <Button Command="{Binding ExportDataCommand}" Style="{StaticResource StandardButtonStyle}" Background="#16A085" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Download" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Export}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding ImportDataCommand}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#8E44AD" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Upload" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Import}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding ShowHierarchyImportCommand}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#A04000" Margin="0,0,5,0" Padding="10,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Sitemap" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.Str_Hierarchy}" FontSize="11"/></StackPanel>
                    </Button>
                    <Button Command="{Binding ShowCreateTableCommand}" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}" Style="{StaticResource StandardButtonStyle}" Background="#D35400" Padding="12,3">
                        <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="PlusCircle" Width="11" Foreground="White" Margin="0,0,6,0"/><TextBlock Text="{x:Static p:Resources.CreateNewTable}" FontSize="11"/></StackPanel>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 3. TOOLS PANEL: Options & Date Filter -->
        <StackPanel Grid.Row="2">
            <Border Visibility="{Binding IsColumnSelectorVisible, Converter={StaticResource BooleanToVisibilityConverter}}" Background="#E6060543" Padding="20,8" BorderBrush="{StaticResource panelActiveColor}" BorderThickness="0,0,0,1">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" Orientation="Horizontal">
                        <StackPanel Margin="0,0,20,0"><TextBlock Text="{x:Static p:Resources.Str_SearchColumn}" Foreground="LightGray" FontSize="10" Margin="0,0,0,2"/><ComboBox ItemsSource="{Binding SearchableColumns}" SelectedItem="{Binding SelectedSearchColumn, Mode=TwoWay}" Width="150" Height="22" FontSize="10"/></StackPanel>
                        <StackPanel Margin="0,0,20,0" VerticalAlignment="Bottom">
                            <CheckBox Content="{x:Static p:Resources.HideId}" IsChecked="{Binding IsIdHidden, Mode=TwoWay}" Foreground="White" FontSize="10" Margin="0,0,0,2"/>
                            <CheckBox Content="{x:Static p:Resources.EditId}" IsChecked="{Binding IsIdEditable, Mode=TwoWay}" Foreground="White" FontSize="10" Margin="0,0,0,2"/>
                            <CheckBox Content="{x:Static p:Resources.Str_HourlyTimeCorrection}" IsChecked="{Binding IsTimeCorrectionEnabled, Mode=TwoWay}" Foreground="LightGreen" FontSize="10" ToolTip="{x:Static p:Resources.Tip_HourlyTimeCorrection}"/>
                        </StackPanel>
                        <Button Command="{Binding RenameColumnCommand}" Visibility="{Binding IsAdminAndOnline, Converter={StaticResource BooleanToVisibilityConverter}}" Background="#2A2A5A" BorderThickness="1" BorderBrush="#40FFFFFF" Padding="8,2" VerticalAlignment="Bottom" Margin="0,0,10,0"><StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Pencil" Width="10" Foreground="White" Margin="0,0,5,0"/><TextBlock Text="{x:Static p:Resources.Str_Rename}" Foreground="White" FontSize="10"/></StackPanel></Button>
                        <StackPanel VerticalAlignment="Bottom" Orientation="Horizontal">
                            <CheckBox Content="{x:Static p:Resources.Str_AdvancedImport}" IsChecked="{Binding IsAdvancedImportVisible, Mode=TwoWay}" Foreground="{StaticResource color6}" FontSize="10" VerticalAlignment="Center" Margin="0,0,10,0"/>
                            <Button Command="{Binding ShowAdvancedImportCommand}" Visibility="{Binding IsAdvancedImportVisible, Converter={StaticResource BooleanToVisibilityConverter}}" Background="#8E44AD" BorderThickness="0" Padding="8,2" Height="22" VerticalAlignment="Center" Margin="0,0,15,0">
                                <StackPanel Orientation="Horizontal"><fa:ImageAwesome Icon="Magic" Width="10" Foreground="White" Margin="0,0,5,0"/><TextBlock Text="{x:Static p:Resources.Str_RunAdvancedImport}" Foreground="White" FontSize="10"/></StackPanel>
                            </Button>
                            <CheckBox Content="{x:Static p:Resources.LoadFullHistory}" IsChecked="{Binding LoadAllData, Mode=TwoWay}" Foreground="LightBlue" FontSize="10" VerticalAlignment="Center" ToolTip="{x:Static p:Resources.Tip_LoadFullHistory}"/>
                        </StackPanel>
                    </StackPanel>
                    <Button Grid.Column="1" Command="{Binding AddIdColumnCommand}" Content="{x:Static p:Resources.AddId}" Background="{StaticResource color5}" Width="110" Height="24" FontSize="10" VerticalAlignment="Bottom" Visibility="{Binding IsOnlineMode, Converter={StaticResource BooleanToVisibilityConverter}}"/>
                </Grid>
            </Border>
            <Border Visibility="{Binding IsDateFilterPanelVisible, Converter={StaticResource BooleanToVisibilityConverter}}" Background="#E6060543" Padding="20,8" BorderBrush="{StaticResource panelActiveColor}" BorderThickness="0,0,0,1">
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" Orientation="Horizontal">
                        <DatePicker SelectedDate="{Binding FilterStartDate, Mode=TwoWay}" Width="130" Height="22" FontSize="10" />
                        <TextBlock Text="{x:Static p:Resources.Str_To}" Foreground="White" VerticalAlignment="Center" Margin="5,0" FontWeight="Bold" FontSize="10"/>
                        <DatePicker SelectedDate="{Binding FilterEndDate, Mode=TwoWay}" Width="130" Height="22" FontSize="10" />
                    </StackPanel>
                    <xctk:RangeSlider Grid.Column="1" Margin="20,0" VerticalAlignment="Center" Minimum="0" Maximum="{Binding SliderMaximum}" LowerValue="{Binding StartMonthSliderValue, Mode=TwoWay}" HigherValue="{Binding EndMonthSliderValue, Mode=TwoWay}" IsSnapToTickEnabled="True" TickFrequency="1" />
                    <Button Grid.Column="2" Content="{x:Static p:Resources.Str_Reset}" Command="{Binding ClearDateFilterCommand}" Style="{StaticResource StandardButtonStyle}" Width="70" Height="22" FontSize="10"/>
                </Grid>
            </Border>
        </StackPanel>

        <!-- 4. MAIN CONTENT: Data Grid -->
        <Grid Grid.Row="3" Margin="5">
            <DataGrid x:Name="DataDisplayGrid"
                      ItemsSource="{Binding DataTableView}"
                      AutoGenerateColumns="True"
                      CanUserAddRows="False"
                      CanUserDeleteRows="False"
                      SelectionMode="Extended"
                      GridLinesVisibility="All"
                      HeadersVisibility="Column"
                      FontSize="{Binding DataGridFontSize, FallbackValue=12}"
                      EnableRowVirtualization="True"
                      EnableColumnVirtualization="True"
                      AutoGeneratingColumn="DataDisplayGrid_AutoGeneratingColumn"
                      BeginningEdit="DataDisplayGrid_BeginningEdit"
                      Sorting="DataDisplayGrid_Sorting"
                      Background="{StaticResource Theme.Grid.Row}"
                      RowBackground="{StaticResource Theme.Grid.Row}"
                      AlternatingRowBackground="#0DFFFFFF"
                      HorizontalGridLinesBrush="{StaticResource Theme.Grid.Lines}"
                      VerticalGridLinesBrush="{StaticResource Theme.Grid.Lines}"
                      Foreground="WhiteSmoke"
                      BorderThickness="1"
                      BorderBrush="{StaticResource Theme.Grid.Lines}">

                <DataGrid.IsReadOnly>
                    <Binding Path="EditableRows" Converter="{StaticResource IsRowEditableConverter}" />
                </DataGrid.IsReadOnly>

                <DataGrid.ColumnHeaderStyle>
                    <Style TargetType="DataGridColumnHeader">
                        <Setter Property="Background" Value="{StaticResource Theme.Grid.Header}" />
                        <Setter Property="Foreground" Value="White" />
                        <Setter Property="Padding" Value="10,6" />
                        <Setter Property="FontWeight" Value="SemiBold" />
                        <Setter Property="FontSize" Value="11" />
                        <Setter Property="BorderThickness" Value="0,0,1,1" />
                        <Setter Property="BorderBrush" Value="{StaticResource Theme.Grid.Lines}" />
                    </Style>
                </DataGrid.ColumnHeaderStyle>

                <DataGrid.RowStyle>
                    <Style TargetType="DataGridRow">
                        <Setter Property="Background" Value="Transparent" />
                        <Style.Triggers>
                            <DataTrigger Value="True">
                                <DataTrigger.Binding>
                                    <MultiBinding Converter="{StaticResource IsItemInCollectionConverter}">
                                        <Binding />
                                        <Binding Path="DataContext.EditableRows" RelativeSource="{RelativeSource AncestorType={x:Type UserControl}}" />
                                    </MultiBinding>
                                </DataTrigger.Binding>
                                <Setter Property="Background" Value="#3D2ECC71" />
                                <Setter Property="Foreground" Value="White" />
                            </DataTrigger>
                            <DataTrigger Binding="{Binding Row.RowState}" Value="{x:Static systemData:DataRowState.Modified}">
                                <Setter Property="Background" Value="#3D3498DB" />
                                <Setter Property="Foreground" Value="White" />
                            </DataTrigger>
                            <DataTrigger Binding="{Binding Row.RowState}" Value="{x:Static systemData:DataRowState.Added}">
                                <Setter Property="Background" Value="#3D16A085" />
                                <Setter Property="Foreground" Value="White" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </DataGrid.RowStyle>
            </DataGrid>

            <Border Background="#80000000" Visibility="{Binding IsProgressBarVisible, Converter={StaticResource BooleanToVisibilityConverter}}" CornerRadius="5">
                <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                    <ProgressBar IsIndeterminate="True" Width="200" Height="6" Foreground="{DynamicResource color5}" Background="Transparent" BorderThickness="0"/>
                    <TextBlock Text="{x:Static p:Resources.Status_Loading}" Foreground="White" HorizontalAlignment="Center" Margin="0,10,0,0" FontWeight="SemiBold"/>
                </StackPanel>
            </Border>
        </Grid>
    </Grid>
</UserControl>
```

---

### File: DatarepView.xaml.cs

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Data;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class DatarepView : UserControl
    {
        public DatarepView()
        {
            InitializeComponent();
        }

        private void DataDisplayGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "ID")
            {
                var visBinding = new System.Windows.Data.Binding("IsIdVisible")
                {
                    Source = this.DataContext,
                    Converter = (System.Windows.Data.IValueConverter)this.Resources["BooleanToVisibilityConverter"]
                };
                System.Windows.Data.BindingOperations.SetBinding(e.Column, DataGridColumn.VisibilityProperty, visBinding);

                var readOnlyBinding = new System.Windows.Data.Binding("IsIdEditable")
                {
                    Source = this.DataContext,
                    Converter = (System.Windows.Data.IValueConverter)this.Resources["BooleanInverter"]
                };
                System.Windows.Data.BindingOperations.SetBinding(e.Column, DataGridColumn.IsReadOnlyProperty, readOnlyBinding);
            }
            else
            {
                e.Column.IsReadOnly = false;
            }

            if (e.PropertyType == typeof(System.DateTime))
            {
                var column = e.Column as DataGridTextColumn;
                if (column != null && column.Binding is System.Windows.Data.Binding originalBinding)
                {
                    var multiBinding = new System.Windows.Data.MultiBinding
                    {
                        Converter = (System.Windows.Data.IMultiValueConverter)this.Resources["DateTimeCorrectionConverter"],
                        Mode = originalBinding.Mode,
                        UpdateSourceTrigger = originalBinding.UpdateSourceTrigger
                    };

                    multiBinding.Bindings.Add(new System.Windows.Data.Binding(e.PropertyName));

                    multiBinding.Bindings.Add(new System.Windows.Data.Binding("IsTimeCorrectionEnabled")
                    {
                        Source = this.DataContext
                    });

                    column.Binding = multiBinding;
                }
            }
        }

        private void DataDisplayGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var viewModel = this.DataContext as DatarepViewModel;
            if (viewModel == null) return;

            var rowView = e.Row.Item as DataRowView;

            if (viewModel.EditableRows == null || !viewModel.EditableRows.Contains(rowView))
            {
                e.Cancel = true;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            DataDisplayGrid.SelectAll();
        }

        private void DataDisplayGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Leave empty to use default DataGrid sorting
        }
    }
}
```

---

### File: DatarepViewModel.cs

```csharp
// ViewModels/DatarepViewModel.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class DatarepViewModel : ViewModelBase, IDisposable
    {
        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;
        private CancellationTokenSource _cts;

        private DataView _dataTableView;
        private string _selectedTable;
        private bool _isBusy;
        private string _errorMessage;
        private bool _isDirty;
        private DataTable _currentDataTable;
        private double _dataGridFontSize = 12;
        private int _longRunningOperationCount = 0;
        private bool _isIdHidden = true;
        private string _dateFilterColumnName;
        private readonly List<string> _dateColumnAliases = new List<string> { "Tarih", "Date", "EntryDate" };
        private readonly List<DataRow> _rowChangeHistory = new List<DataRow>();
        private int _nextNewRowId = -1;

        private string _searchText;
        private string _selectedSearchColumn;
        private bool _isGlobalSearchActive;
        private bool _isUpdatingDates;
        private DateTime _minSliderDate;

        public ObservableCollection<string> TableNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<DataRowView> EditableRows { get; } = new ObservableCollection<DataRowView>();
        public ObservableCollection<string> SearchableColumns { get; } = new ObservableCollection<string>();

        public bool IsOfflineMode => _dataRepository is OfflineDataRepository;
        public bool IsOnlineMode => !IsOfflineMode;
        public bool IsAdminAndOnline => IsAdmin && IsOnlineMode;
        public bool IsAdmin => UserSessionService.IsAdmin;
        public bool IsBusy 
        { 
            get => _isBusy; 
            private set 
            { 
                if (SetProperty(ref _isBusy, value)) 
                {
                    OnPropertyChanged(nameof(IsProgressBarVisible));
                    (AddNewRowCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (ReloadDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            } 
        }
        public bool IsProgressBarVisible => IsBusy;

        public string ErrorMessage
        { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool IsDirty
        { get => _isDirty; private set { if (SetProperty(ref _isDirty, value)) (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); } }

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    _cts?.Cancel();
                    EditableRows.Clear();
                    UnsubscribeFromTableEvents();
                    _ = LoadDataForSelectedTableAsync();
                }
            }
        }

        private bool _loadAllData;
        public bool LoadAllData
        { get => _loadAllData; set { if (SetProperty(ref _loadAllData, value)) _ = LoadDataForSelectedTableAsync(); } }

        public DataView DataTableView
        {
            get => _dataTableView;
            private set
            {
                if (SetProperty(ref _dataTableView, value))
                {
                    _currentDataTable = _dataTableView?.Table;
                    SubscribeToTableEvents();
                    IsDirty = false;
                    PopulateSearchableColumns();
                }
            }
        }

        public string SearchText
        { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyCombinedFiltersAsync(); } }

        public string SelectedSearchColumn
        { get => _selectedSearchColumn; set { if (SetProperty(ref _selectedSearchColumn, value)) { _isGlobalSearchActive = false; ApplyCombinedFiltersAsync(); } } }

        public bool IsGlobalSearchActive
        { get => _isGlobalSearchActive; set { if (SetProperty(ref _isGlobalSearchActive, value)) ApplyCombinedFiltersAsync(); } }

        private bool _isDateFilterVisible;
        private bool _isDateFilterPanelVisible;
        private bool _isColumnSelectorVisible;
        private bool _isAdvancedImportVisible;
        private DateTime? _filterStartDate, _filterEndDate;
        private double _sliderMax, _sliderStart, _sliderEnd;

        public bool IsDateFilterVisible { get => _isDateFilterVisible; private set => SetProperty(ref _isDateFilterVisible, value); }
        public bool IsDateFilterPanelVisible { get => _isDateFilterPanelVisible; set => SetProperty(ref _isDateFilterPanelVisible, value); }
        public bool IsColumnSelectorVisible { get => _isColumnSelectorVisible; set => SetProperty(ref _isColumnSelectorVisible, value); }
        public bool IsAdvancedImportVisible { get => _isAdvancedImportVisible; set => SetProperty(ref _isAdvancedImportVisible, value); }

        public DateTime? FilterStartDate
        { get => _filterStartDate; set { if (SetProperty(ref _filterStartDate, value)) { ApplyCombinedFiltersAsync(); UpdateSlidersFromDates(); } } }

        public DateTime? FilterEndDate
        { get => _filterEndDate; set { if (SetProperty(ref _filterEndDate, value)) { ApplyCombinedFiltersAsync(); UpdateSlidersFromDates(); } } }

        public double SliderMaximum { get => _sliderMax; set => SetProperty(ref _sliderMax, value); }

        public double StartMonthSliderValue
        { get => _sliderStart; set { if (SetProperty(ref _sliderStart, value)) UpdateDatesFromSliders(); } }

        public double EndMonthSliderValue
        { get => _sliderEnd; set { if (SetProperty(ref _sliderEnd, value)) UpdateDatesFromSliders(); } }

        private bool _isTimeCorrectionEnabled;
        public bool IsTimeCorrectionEnabled
        { get => _isTimeCorrectionEnabled; set { if (SetProperty(ref _isTimeCorrectionEnabled, value)) OnPropertyChanged(nameof(IsTimeCorrectionEnabled)); } }

        public bool IsIdHidden
        { get => _isIdHidden; set { if (SetProperty(ref _isIdHidden, value)) OnPropertyChanged(nameof(IsIdVisible)); } }

        private bool _isIdEditable;
        public bool IsIdEditable
        { get => _isIdEditable; set { if (SetProperty(ref _isIdEditable, value)) OnPropertyChanged(nameof(IsIdEditable)); } }

        public bool IsIdVisible => !_isIdHidden;

        public double DataGridFontSize
        { get => _dataGridFontSize; set { if (SetProperty(ref _dataGridFontSize, Math.Max(8, Math.Min(24, value)))) { (DecreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (IncreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); } } }

        public ICommand AddNewRowCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand UndoChangesCommand { get; }
        public ICommand EditSelectedRowsCommand { get; }
        public ICommand ReloadDataCommand { get; }
        public ICommand DeleteSelectedRowCommand { get; }
        public ICommand DeleteTableCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ShowAdvancedImportCommand { get; }
        public ICommand ShowCreateTableCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ClearDateFilterCommand { get; }
        public ICommand AddIdColumnCommand { get; }
        public ICommand ShowHierarchyImportCommand { get; }
        public ICommand RenameColumnCommand { get; }
        public ICommand ShowFindReplaceCommand { get; }
        public ICommand DecreaseFontSizeCommand { get; }
        public ICommand IncreaseFontSizeCommand { get; }

        public DatarepViewModel(ILogger logger, IDialogService dialogService, IDataRepository dataRepository)
        {
            _logger = logger;
            _dialogService = dialogService;
            _dataRepository = dataRepository;

            AddNewRowCommand = new ViewModelCommand(ExecuteAddNewRow, p => _currentDataTable != null && !IsBusy && IsOnlineMode);
            SaveChangesCommand = new ViewModelCommand(ExecuteSaveChanges, p => IsDirty && !IsBusy && IsOnlineMode);
            UndoChangesCommand = new ViewModelCommand(ExecuteUndo, p => _rowChangeHistory.Any() && !IsBusy && IsOnlineMode);
            EditSelectedRowsCommand = new ViewModelCommand(ExecuteEditRows, p => IsOnlineMode);
            DeleteSelectedRowCommand = new ViewModelCommand(ExecuteDeleteRow, p => p is IList i && i.Count > 0 && !IsBusy && IsOnlineMode);
            DeleteTableCommand = new ViewModelCommand(ExecuteDeleteTable, p => !string.IsNullOrEmpty(SelectedTable) && IsAdminAndOnline && !IsBusy);

            ImportDataCommand = new ViewModelCommand(ExecuteImportData, p => _currentDataTable != null && !IsBusy && IsOnlineMode);
            ShowAdvancedImportCommand = new ViewModelCommand(p => { var vm = new ImportTableViewModel(SelectedTable, _dialogService); if (_dialogService.ShowImportTableDialog(vm, out ImportSettings s)) ExecuteImportData(s); }, p => _currentDataTable != null && !IsBusy && IsOnlineMode);
            AddIdColumnCommand = new ViewModelCommand(async p => await ExecuteLongRunning(async t => { var r = await _dataRepository.AddPrimaryKeyAsync(SelectedTable); if (r.Success) await LoadDataForSelectedTableAsync(); }), p => _currentDataTable != null && !_currentDataTable.Columns.Contains("ID") && IsOnlineMode);
            ShowCreateTableCommand = new ViewModelCommand(p => { _dialogService.ShowCreateTableDialog(new CreateTableViewModel(_dialogService, _logger, _dataRepository)); LoadInitialDataAsync(); }, p => IsOnlineMode);
            RenameColumnCommand = new ViewModelCommand(ExecuteRenameColumn, p => !IsBusy && IsAdminAndOnline && !string.IsNullOrEmpty(SelectedSearchColumn));
            ShowHierarchyImportCommand = new ViewModelCommand(p => _dialogService.ShowHierarchyImportDialog(new HierarchyImportViewModel(_dataRepository, _dialogService, _logger) { SelectedTableName = SelectedTable }), p => IsOnlineMode);

            ReloadDataCommand = new ViewModelCommand(p => _ = ExecuteLongRunning(async t => { await Task.Delay(300); await LoadDataForSelectedTableAsync(); }), p => !string.IsNullOrEmpty(SelectedTable) && !IsBusy);
            ExportDataCommand = new ViewModelCommand(ExecuteExportData, p => _currentDataTable?.Rows.Count > 0 && !IsBusy);

            ShowFindReplaceCommand = new ViewModelCommand(ExecuteShowFindReplace, p => _currentDataTable != null && !IsBusy);

            DecreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize--, p => DataGridFontSize > 8);
            IncreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize++, p => DataGridFontSize < 24);
            ClearSearchCommand = new ViewModelCommand(p => { SearchText = ""; IsGlobalSearchActive = false; });
            ClearDateFilterCommand = new ViewModelCommand(p => { FilterStartDate = null; FilterEndDate = null; IsDateFilterPanelVisible = false; ApplyCombinedFiltersAsync(); });

            DatabaseRetryPolicy.OnRetryStatus += OnRetryStatusReceived;
            LoadInitialDataAsync();
        }

        private void OnRetryStatusReceived(string msg) => Application.Current?.Dispatcher.Invoke(() => SetErrorMessage(msg));

        private void ExecuteEditRows(object parameter)
        {
            if (parameter is System.Collections.IList items)
            {
                if (items.Count == 0) return;
                EditableRows.Clear();
                foreach (var item in items)
                {
                    if (item is System.Data.DataRowView drv) EditableRows.Add(drv);
                }
                OnPropertyChanged(nameof(EditableRows));
                (EditSelectedRowsCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void ExecuteUndo(object parameter)
        {
            var lastRow = _rowChangeHistory.LastOrDefault();
            if (lastRow != null)
            {
                lastRow.RejectChanges();
                _rowChangeHistory.Remove(lastRow);
                CheckIfDirty();
            }
        }

        private async Task ExecuteLongRunning(Func<CancellationToken, Task> op)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Interlocked.Increment(ref _longRunningOperationCount);
            IsBusy = true;
            SetErrorMessage(null);

            try { await op(token); }
            catch (Exception ex) { _logger.LogError("[Op]", ex); SetErrorMessage($"Error: {ex.Message}"); }
            finally
            {
                if (Interlocked.Decrement(ref _longRunningOperationCount) == 0) IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void LoadInitialDataAsync() => await ExecuteLongRunning(async t =>
        {
            var names = await _dataRepository.GetTableNamesAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TableNames.Clear();
                foreach (var n in names) TableNames.Add(n);
                if (!TableNames.Contains(SelectedTable)) SelectedTable = TableNames.FirstOrDefault();
            });
        });

        private async Task LoadDataForSelectedTableAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            string target = SelectedTable;

            await ExecuteLongRunning(async token =>
            {
                var res = await _dataRepository.GetTableDataAsync(target, LoadAllData ? 0 : GeneralSettingsManager.Instance.Current.DefaultRowLimit);
                if (token.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (SelectedTable != target) return;

                    if (res.Data != null && res.Data.Columns.Contains("ID"))
                        res.Data.PrimaryKey = new[] { res.Data.Columns["ID"] };

                    DataTableView = res.Data?.DefaultView;
                    _nextNewRowId = -1;

                    if (!res.IsSortable) SetErrorMessage("⚠️ No ID/Date column. Editing limited.");

                    SetupDateFilter();
                    ApplyCombinedFiltersAsync();
                });
            });
        }

        public async void LoadTableWithFilter(string t, DateTime s, DateTime e, string txt = "")
        {
            _selectedTable = t;
            OnPropertyChanged(nameof(SelectedTable));

            _cts?.Cancel();
            EditableRows.Clear();
            UnsubscribeFromTableEvents();

            await LoadDataForSelectedTableAsync();

            if (DataTableView != null)
            {
                FilterStartDate = s;
                FilterEndDate = e;
                if (!string.IsNullOrEmpty(txt))
                {
                    IsGlobalSearchActive = true;
                    SearchText = txt;
                }
                IsDateFilterVisible = true;
                ApplyCombinedFiltersAsync();
            }
        }

        private async void ApplyCombinedFiltersAsync()
        {
            if (DataTableView == null) return;
            string txt = SearchText, col = SelectedSearchColumn, dateCol = _dateFilterColumnName;
            bool global = IsGlobalSearchActive, hasDate = IsDateFilterVisible && _filterStartDate.HasValue && _filterEndDate.HasValue;
            DateTime? s = _filterStartDate, e = _filterEndDate;

            await Task.Run(() =>
            {
                string filter = DataImportExportHelper.BuildFilterString(_currentDataTable, txt, col, global, hasDate, dateCol, s, e);
                Application.Current.Dispatcher.Invoke(() => { try { DataTableView.RowFilter = filter; } catch { } });
            });
        }

        private async void ExecuteSaveChanges(object p)
        {
            var changes = _currentDataTable?.GetChanges();
            if (changes == null) return;
            if (!_dialogService.ShowConfirmationDialog(Resources.Save, $"Save {changes.Rows.Count} changes?")) return;

            bool newRows = changes.AsEnumerable().Any(r => r.RowState == DataRowState.Added);

            await ExecuteLongRunning(async t =>
            {
                var r = await _dataRepository.SaveChangesAsync(changes, SelectedTable);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (r.Success)
                    {
                        _currentDataTable.AcceptChanges();
                        EditableRows.Clear();
                        CheckIfDirty();
                        if (newRows) _ = LoadDataForSelectedTableAsync();
                        SetErrorMessage("Changes Saved Successfully.");
                    }
                    else SetErrorMessage(r.ErrorMessage);
                });
            });
        }

        private async void ExecuteImportData(object p)
        {
            ImportSettings s = p as ImportSettings;
            if (s == null && p == null)
            {
                if (_dialogService.ShowOpenFileDialog("Import", "Excel/CSV|*.xlsx;*.csv", out string f))
                    s = new ImportSettings { FilePath = f };
                else return;
            }
            if (s == null) return;

            await ExecuteLongRunning(async t =>
            {
                var res = DataImportExportHelper.ImportDataToTable(s.FilePath, _currentDataTable, s.RowsToIgnore);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetErrorMessage(res.Message);
                    CheckIfDirty();
                });
            });
        }

        private async void ExecuteExportData(object p)
        {
            if (_dialogService.ShowSaveFileDialog("Export", $"{SelectedTable}_{DateTime.Now:yyyyMMdd}", ".xlsx", "Excel|*.xlsx|CSV|*.csv", out string path))
            {
                await ExecuteLongRunning(async t => await Task.Run(() => DataImportExportHelper.ExportTable(path, _currentDataTable, SelectedTable)));
            }
        }

        private void SetupDateFilter()
        {
            IsDateFilterVisible = false; _minSliderDate = default;
            var dc = _currentDataTable?.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(DateTime) && _dateColumnAliases.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase))
                  ?? _currentDataTable?.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(DateTime));

            if (dc != null)
            {
                var dates = _currentDataTable.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted && r[dc] != DBNull.Value).Select(r => (DateTime)r[dc]).ToList();
                if (dates.Any())
                {
                    _minSliderDate = dates.Min(); _dateFilterColumnName = dc.ColumnName;
                    SliderMaximum = ((dates.Max().Year - _minSliderDate.Year) * 12) + dates.Max().Month - _minSliderDate.Month;
                    IsDateFilterVisible = true;
                    FilterStartDate = dates.Min(); FilterEndDate = dates.Max();
                }
            }
        }

        private void UpdateSlidersFromDates()
        {
            if (!IsDateFilterVisible || _isUpdatingDates || _minSliderDate == default || FilterStartDate == null || FilterEndDate == null) return;
            _isUpdatingDates = true;
            try
            {
                StartMonthSliderValue = Math.Max(0, ((FilterStartDate.Value.Year - _minSliderDate.Year) * 12) + FilterStartDate.Value.Month - _minSliderDate.Month);
                EndMonthSliderValue = Math.Min(SliderMaximum, ((FilterEndDate.Value.Year - _minSliderDate.Year) * 12) + FilterEndDate.Value.Month - _minSliderDate.Month);
            }
            finally { _isUpdatingDates = false; }
        }

        private void UpdateDatesFromSliders()
        {
            if (!IsDateFilterVisible || _isUpdatingDates || _minSliderDate == default) return;
            _isUpdatingDates = true;
            try
            {
                if (StartMonthSliderValue > EndMonthSliderValue) StartMonthSliderValue = EndMonthSliderValue;
                FilterStartDate = _minSliderDate.AddMonths((int)StartMonthSliderValue);
                var endBase = _minSliderDate.AddMonths((int)EndMonthSliderValue);
                FilterEndDate = new DateTime(endBase.Year, endBase.Month, DateTime.DaysInMonth(endBase.Year, endBase.Month));
                ApplyCombinedFiltersAsync();
            }
            finally { _isUpdatingDates = false; }
        }

        private void ExecuteAddNewRow(object p)
        {
            NewRowData data; bool ok;
            if (SelectedTable.StartsWith("_Long_", StringComparison.OrdinalIgnoreCase))
                ok = _dialogService.ShowAddRowLongDialog(new AddRowLongViewModel(SelectedTable, _dataRepository, _logger, _dialogService), out data);
            else
                ok = _dialogService.ShowAddRowDialog(_currentDataTable.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement && !c.ReadOnly).Select(c => c.ColumnName), SelectedTable, null, _currentDataTable, IsIdHidden, out data);

            if (ok && data != null)
            {
                try
                {
                    var r = _currentDataTable.NewRow();
                    foreach (var k in data.Values.Keys) if (_currentDataTable.Columns.Contains(k)) r[k] = data.Values[k] ?? DBNull.Value;

                    if (_currentDataTable.Columns.Contains("ID") && (r["ID"] == DBNull.Value || r["ID"] == null))
                    {
                        var idCol = _currentDataTable.Columns["ID"];
                        if (idCol.DataType == typeof(Guid)) r["ID"] = Guid.NewGuid();
                        else
                        {
                            r["ID"] = Convert.ChangeType(_nextNewRowId, idCol.DataType);
                            _nextNewRowId--;
                        }
                    }
                    _currentDataTable.Rows.Add(r);
                    CheckIfDirty();
                }
                catch (Exception ex) { SetErrorMessage(ex.Message); }
            }
        }

        private void ExecuteDeleteRow(object p)
        { if (_dialogService.ShowConfirmationDialog("Delete", "Delete selected rows?")) { foreach (var r in ((IList)p).OfType<DataRowView>().ToList()) { r.Row.Delete(); } CheckIfDirty(); } }

        private async void ExecuteDeleteTable(object p)
        { if (_dialogService.ShowConfirmationDialog("Delete", "Permanently Delete Table?")) await ExecuteLongRunning(async t => { if (await _dataRepository.DeleteTableAsync(SelectedTable)) LoadInitialDataAsync(); }); }

        private async void ExecuteRenameColumn(object p)
        { if (_dialogService.ShowInputDialog("Rename", $"Rename '{SelectedSearchColumn}' to:", SelectedSearchColumn, out string n)) await ExecuteLongRunning(async t => { var r = await _dataRepository.RenameColumnAsync(SelectedTable, SelectedSearchColumn, n); if (r.Success) await LoadDataForSelectedTableAsync(); else SetErrorMessage(r.ErrorMessage); }); }

        private void ExecuteShowFindReplace(object p)
        {
            var w = new FindReplaceWindow();

            w.FindRequested += (s, e) =>
            {
                SearchText = w.FindText;
                IsGlobalSearchActive = true;
            };

            w.ReplaceRequested += (s, e) =>
            {
                if (string.IsNullOrEmpty(w.FindText) || _currentDataTable == null || _dataTableView == null) return;

                int replaceCount = 0;
                string find = w.FindText;
                string replace = w.ReplaceText ?? "";
                bool matchCase = w.MatchCase;

                foreach (DataRowView drv in _dataTableView)
                {
                    DataRow row = drv.Row;
                    var cols = _isGlobalSearchActive
                        ? _currentDataTable.Columns.Cast<DataColumn>()
                        : new[] { _currentDataTable.Columns[SelectedSearchColumn] }.Where(c => c != null);

                    foreach (var col in cols)
                    {
                        if (col.ReadOnly || col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue;

                        if (row[col] != DBNull.Value && row[col] != null && col.DataType == typeof(string))
                        {
                            string val = row[col].ToString();
                            System.StringComparison comp = matchCase ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;

                            if (val.IndexOf(find, comp) >= 0)
                            {
                                string newVal = matchCase
                                    ? val.Replace(find, replace)
                                    : System.Text.RegularExpressions.Regex.Replace(
                                        val,
                                        System.Text.RegularExpressions.Regex.Escape(find),
                                        replace.Replace("$", "$$"),
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                try
                                {
                                    row[col] = newVal;
                                    replaceCount++;
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (replaceCount > 0)
                {
                    CheckIfDirty();
                }

                SetErrorMessage($"Replaced {replaceCount} occurrences.");
            };

            if (Application.Current.MainWindow != null) w.Owner = Application.Current.MainWindow;
            w.Show();
        }

        private void CurrentDataTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (e.Action != DataRowAction.Commit) { _rowChangeHistory.Add(e.Row); CheckIfDirty(); }
        }

        private void SubscribeToTableEvents()
        { if (_currentDataTable != null) _currentDataTable.RowChanged += CurrentDataTable_RowChanged; }

        private void UnsubscribeFromTableEvents()
        { if (_currentDataTable != null) _currentDataTable.RowChanged -= CurrentDataTable_RowChanged; }

        private void CheckIfDirty() => IsDirty = _currentDataTable?.GetChanges() != null;

        private void SetErrorMessage(string m) => ErrorMessage = m;

        private void PopulateSearchableColumns()
        { SearchableColumns.Clear(); if (_currentDataTable != null) foreach (DataColumn c in _currentDataTable.Columns) SearchableColumns.Add(c.ColumnName); SelectedSearchColumn = SearchableColumns.FirstOrDefault(); }

        public void Dispose()
        {
            DatabaseRetryPolicy.OnRetryStatus -= OnRetryStatusReceived;
            UnsubscribeFromTableEvents();
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
```

---

### File: ViewModelBase.cs

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_LoginForm.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
```

---

### File: ViewModelCommand.cs

```csharp
using System;
using System.Windows.Input;

namespace WPF_LoginForm.ViewModels
{
    public class ViewModelCommand : ICommand
    {
        private readonly Action<object> _executeAction;
        private readonly Predicate<object> _canExecuteAction;

        public ViewModelCommand(Action<object> executeAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = null;
        }

        public ViewModelCommand(Action<object> executeAction, Predicate<object> canExecuteAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = canExecuteAction;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteAction == null ? true : _canExecuteAction(parameter);
        }

        public void Execute(object parameter)
        {
            _executeAction(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
```

---

### File: BooleanToVisibilityConverter.cs

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
            {
                return visibilityValue == Visibility.Visible;
            }
            return false;
        }
    }
}
```

---

### File: BooleanInverterConverter.cs

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BooleanInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
```

---

### File: IsRowEditableConverter.cs

```csharp
using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class IsRowEditableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICollection collection)
            {
                if (collection.Count > 0)
                {
                    return false;
                }
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

---

### File: IsItemInCollectionConverter.cs

```csharp
using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class IsItemInCollectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return false;

            var item = values[0];
            var collection = values[1] as IList;

            if (collection != null && collection.Contains(item))
            {
                return true;
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

---

### File: IsNullOrEmptyConverter.cs

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    [ValueConversion(typeof(string), typeof(bool))]
    public class IsNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

---

### File: DateTimeCorrectionConverter.cs

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class DateTimeCorrectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is DateTime dt) || !(values[1] is bool isEnabled))
                return values.Length > 0 ? values[0] : null;

            if (isEnabled && dt.Year == 1899 && dt.Month == 12 && dt.Day == 30)
            {
                return dt.ToString("HH:mm");
            }

            return dt.ToString("dd/MM/yyyy HH:mm");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new[] { value };
        }
    }
}
```

---

### File: ILogger.cs

```csharp
using System;

namespace WPF_LoginForm.Services
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
    }
}
```

---

### File: IDialogService.cs

```csharp
using System.Collections.Generic;
using System.Data;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Services
{
    public interface IDialogService
    {
        bool ShowAddRowDialog(IEnumerable<string> columnNames, string tableName,
                              Dictionary<string, object> initialValues,
                              DataTable sourceTable,
                              bool hideId,
                              out NewRowData newRowData);

        bool ShowAddRowLongDialog(AddRowLongViewModel viewModel, out NewRowData newRowData);

        bool ShowConfigurationDialog(ConfigurationViewModel viewModel);

        bool ShowImportTableDialog(ImportTableViewModel viewModel, out ImportSettings settings);

        void ShowCreateTableDialog(CreateTableViewModel viewModel);

        void ShowHierarchyImportDialog(HierarchyImportViewModel viewModel);

        bool ShowConfirmationDialog(string title, string message);

        bool ShowSaveFileDialog(string title, string defaultFileName, string defaultExtension, string filter, out string selectedFilePath);

        bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath);

        bool ShowInputDialog(string title, string message, string defaultValue, out string result);
    }
}
```

---

### File: IDataRepository.cs

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public interface IDataRepository
    {
        Task<List<string>> GetTableNamesAsync();
        Task<bool> TableExistsAsync(string tableName);
        Task<string> GetActualColumnNameAsync(string tableName, string p1, string p2, string p3, string p4, string coreItem);
        Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn);
        Task<(bool Success, string ErrorMessage)> RenameColumnAsync(string tableName, string oldName, string newName);
        Task<(DataTable Data, bool IsSortable)> GetTableDataAsync(string tableName, int limit = 0);
        Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate);
        Task<List<ErrorEventModel>> GetErrorDataAsync(DateTime startDate, DateTime endDate, string tableName);
        Task<DataTable> GetSystemLogsAsync();
        Task<bool> ClearSystemLogsAsync();
        Task<List<string>> GetDistinctPart1ValuesAsync(string tableName);
        Task<List<string>> GetDistinctPart2ValuesAsync(string tableName, string p1);
        Task<List<string>> GetDistinctPart3ValuesAsync(string tableName, string p1, string p2);
        Task<List<string>> GetDistinctPart4ValuesAsync(string tableName, string p1, string p2, string p3);
        Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string tableName, string p1, string p2, string p3, string p4);
        Task<bool> ClearHierarchyMapForTableAsync(string tableName);
        Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData);
        Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName);
        Task<bool> DeleteTableAsync(string tableName);
        Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName);
        Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema);
        Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data);
    }
}
```

---

### File: NewRowData.cs

```csharp
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class NewRowData
    {
        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
    }
}
```

---

### File: ImportSettings.cs

```csharp
namespace WPF_LoginForm.Models
{
    public class ImportSettings
    {
        public string FilePath { get; set; }
        public int RowsToIgnore { get; set; }
    }
}
```

---

### File: GeneralSettings.cs

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WPF_LoginForm.Models
{
    public class GeneralSettings
    {
        public string DbProvider { get; set; } = "SqlServer";
        public string SqlAuthConnString { get; set; } = "Server=(local); Database=LoginDb; Integrated Security=true";
        public string SqlDataConnString { get; set; } = "Server=(local); Database=MainDataDb; Integrated Security=true";
        public string PostgresDataConnString { get; set; } = "Host=localhost; Username=postgres; Password=password; Database=MainDataDb";
        public string PostgresAuthConnString { get; set; } = "Host=localhost; Username=postgres; Password=password; Database=LoginDb";

        public string AppLanguage { get; set; } = "en-US";
        public string OfflineFolderPath { get; set; } = "";

        public bool AutoImportEnabled { get; set; } = false;
        public bool ImportIsRelative { get; set; } = true;
        public string ImportFileName { get; set; } = "dashboard_config.json";
        public string ImportAbsolutePath { get; set; } = "";

        [JsonIgnore] public bool ShowDashboardDateFilter { get; set; } = true;
        [JsonIgnore] public int DashboardDateTickSize { get; set; } = 1;
        [JsonIgnore] public int DefaultRowLimit { get; set; } = 500;

        public int ConnectionTimeout { get; set; } = 15;
        public bool TrustServerCertificate { get; set; } = true;
        public string DbServerName { get; set; } = "";

        public string DbHost { get; set; } = "localhost";
        public string DbPort { get; set; } = "1433";
        public string DbUser { get; set; } = "admin";

        [JsonIgnore] public List<CategoryRule> CategoryRules { get; set; } = new List<CategoryRule>();
    }
}
```

---

### File: DataImportExportHelper.cs

```csharp
// Services/DataImportExportHelper.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OfficeOpenXml;

namespace WPF_LoginForm.Services
{
    public static class DataImportExportHelper
    {
        public class ImportResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int Added { get; set; }
            public int Skipped { get; set; }
        }

        public static string BuildFilterString(DataTable table, string text, string colName, bool global, bool hasDate, string dateCol, DateTime? start, DateTime? end)
        {
            if (table == null) return "";
            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(text))
            {
                string safe = text.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                if (global)
                {
                    var sub = table.Columns.Cast<DataColumn>().Select(c => $"Convert([{c.ColumnName}], 'System.String') LIKE '%{safe}%'");
                    if (sub.Any()) filters.Add($"({string.Join(" OR ", sub)})");
                }
                else if (!string.IsNullOrEmpty(colName) && table.Columns.Contains(colName))
                {
                    string safeCol = colName.Replace("]", "]]");
                    string opFilter = "";
                    if (text.Trim().StartsWith(">") || text.Trim().StartsWith("<"))
                    {
                        var col = table.Columns[colName];
                        var op = text.Trim().StartsWith(">=") || text.Trim().StartsWith("<=") ? text.Trim().Substring(0, 2) : text.Trim().Substring(0, 1);
                        if (double.TryParse(text.Trim().Substring(op.Length), NumberStyles.Any, CultureInfo.CurrentCulture, out double val))
                        {
                            if (col.DataType == typeof(int) || col.DataType == typeof(double) || col.DataType == typeof(decimal))
                                opFilter = $"[{safeCol}] {op} {val.ToString(CultureInfo.InvariantCulture)}";
                        }
                    }
                    filters.Add(string.IsNullOrEmpty(opFilter) ? $"Convert([{safeCol}], 'System.String') LIKE '%{safe}%'" : opFilter);
                }
            }

            if (hasDate && !string.IsNullOrEmpty(dateCol) && start.HasValue && end.HasValue && table.Columns.Contains(dateCol))
            {
                string safeD = dateCol.Replace("]", "]]");
                filters.Add($"[{safeD}] >= #{start.Value:MM/dd/yyyy}# AND [{safeD}] <= #{end.Value:MM/dd/yyyy}#");
            }

            return string.Join(" AND ", filters);
        }

        public static ImportResult ImportDataToTable(string filePath, DataTable targetTable, int rowsToIgnore)
        {
            var res = new ImportResult();
            var errors = new List<string>();
            DataTable importDt = null;

            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == ".xlsx") importDt = LoadXlsx(filePath, errors, rowsToIgnore);
                else if (ext == ".csv") importDt = LoadCsv(filePath, errors, rowsToIgnore);

                if (importDt != null && importDt.Rows.Count > 0)
                {
                    foreach (DataRow sRow in importDt.Rows)
                    {
                        try
                        {
                            var newRow = targetTable.NewRow();
                            foreach (DataColumn tCol in targetTable.Columns)
                            {
                                if (tCol.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue;

                                var sCol = importDt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals(tCol.ColumnName, StringComparison.OrdinalIgnoreCase));
                                if (sCol != null)
                                {
                                    object val = sRow[sCol];
                                    if (val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString()))
                                    {
                                        if (!tCol.AllowDBNull) throw new Exception("Required");
                                        newRow[tCol] = DBNull.Value;
                                    }
                                    else
                                    {
                                        if (tCol.DataType == typeof(DateTime)) newRow[tCol] = ParseDate(val) ?? (object)DBNull.Value;
                                        else if (tCol.DataType == typeof(bool)) newRow[tCol] = ParseBool(val.ToString());
                                        else if (tCol.DataType == typeof(Guid)) newRow[tCol] = Guid.Parse(val.ToString());
                                        else newRow[tCol] = Convert.ChangeType(val, tCol.DataType, CultureInfo.CurrentCulture);
                                    }
                                }
                            }
                            targetTable.Rows.Add(newRow);
                            res.Added++;
                        }
                        catch { res.Skipped++; }
                    }
                    res.Success = true;
                    res.Message = $"Imported: {res.Added}, Skipped: {res.Skipped}.";
                }
                else res.Message = "No data found or file empty.";
            }
            catch (Exception ex) { res.Message = $"File Error: {ex.Message}"; }
            return res;
        }

        public static void ExportTable(string path, DataTable table, string sheetName)
        {
            var rows = table.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted);
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var p = new ExcelPackage(new FileInfo(path)))
                {
                    var ws = p.Workbook.Worksheets.Add(Sanitize(sheetName));
                    if (rows.Any())
                    {
                        ws.Cells["A1"].LoadFromDataTable(rows.CopyToDataTable(), true);
                        ws.Cells.AutoFitColumns();
                    }
                    p.Save();
                }
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName))));
                foreach (var r in rows)
                {
                    sb.AppendLine(string.Join(";", r.ItemArray.Select(i => Quote(i?.ToString()))));
                }
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
        }

        private static DataTable LoadXlsx(string path, List<string> err, int skip)
        {
            var dt = new DataTable();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var p = new ExcelPackage(stream))
            {
                var ws = p.Workbook.Worksheets.FirstOrDefault();
                if (ws == null || ws.Dimension == null) return null;
                int start = 1 + skip;
                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                {
                    string h = ws.Cells[start, c].Text.Trim();
                    dt.Columns.Add(string.IsNullOrEmpty(h) ? $"Col{c}" : h);
                }
                for (int r = start + 1; r <= ws.Dimension.End.Row; r++)
                {
                    var row = dt.NewRow();
                    bool hasVal = false;
                    for (int c = 1; c <= dt.Columns.Count; c++)
                    {
                        var val = ws.Cells[r, c].Value;
                        if (val != null) hasVal = true;
                        row[c - 1] = val ?? DBNull.Value;
                    }
                    if (hasVal) dt.Rows.Add(row);
                }
            }
            return dt;
        }

        private static DataTable LoadCsv(string path, List<string> err, int skip)
        {
            var dt = new DataTable();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                for (int i = 0; i < skip; i++)
                {
                    if (reader.ReadLine() == null) return dt;
                }
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine)) return dt;

                char delimiter = ',';
                int commaCount = headerLine.Count(c => c == ',');
                int semiCount = headerLine.Count(c => c == ';');
                if (semiCount > commaCount) delimiter = ';';

                string pattern = Regex.Escape(delimiter.ToString()) + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)";
                var headers = Regex.Split(headerLine, pattern);
                foreach (var h in headers) dt.Columns.Add(h.Trim('"'));

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = dt.NewRow();
                    var vals = Regex.Split(line, pattern);
                    for (int i = 0; i < Math.Min(vals.Length, dt.Columns.Count); i++)
                    {
                        row[i] = vals[i].Trim('"');
                    }
                    dt.Rows.Add(row);
                }
            }
            return dt;
        }

        private static DateTime? ParseDate(object v)
        {
            if (v == null) return null;
            if (v is DateTime d) return d;
            if (v is double dbl) return DateTime.FromOADate(dbl);
            if (DateTime.TryParse(v.ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dt)) return dt;
            if (DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private static bool ParseBool(string v) => v?.ToLower() == "true" || v == "1" || v?.ToLower() == "yes" || v?.ToLower() == "on";

        private static string Quote(string v) => (v?.Contains(";") == true || v?.Contains(",") == true || v?.Contains("\"") == true || v?.Contains("\n") == true) ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

        private static string Sanitize(string s) => Regex.Replace(s ?? "Sheet1", @"[\\/\?\*\[\]:]", "_");
    }
}
```

---

### File: GeneralSettingsManager.cs

```csharp
// Services/GeneralSettingsManager.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.Services
{
    public class GeneralSettingsManager
    {
        private static GeneralSettingsManager _instance;
        public static GeneralSettingsManager Instance => _instance ?? (_instance = new GeneralSettingsManager());

        private readonly string _configLocationPointerFile;
        public GeneralSettings Current { get; private set; }

        private GeneralSettingsManager()
        {
            _configLocationPointerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_location.txt");
            Load();
        }

        public string GetResolvedConfigPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "general_config.json");
            if (File.Exists(localPath)) return localPath;

            if (File.Exists(_configLocationPointerFile))
            {
                try
                {
                    string customPath = File.ReadAllText(_configLocationPointerFile).Trim();
                    if (!string.IsNullOrEmpty(customPath))
                    {
                        if (Directory.Exists(customPath))
                            return Path.Combine(customPath, "general_config.json");
                        else if (customPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            return customPath;
                    }
                }
                catch { }
            }
            return localPath;
        }

        public void SetCustomConfigPath(string newPath)
        {
            File.WriteAllText(_configLocationPointerFile, newPath);
            MessageBox.Show("Save File successfully changed. Restart of app is needed.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        public void Load()
        {
            string configPath = GetResolvedConfigPath();
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    Current = JsonConvert.DeserializeObject<GeneralSettings>(json) ?? new GeneralSettings();
                    LoadDashboardPart();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading general config: {ex.Message}");
                    LoadFromLegacyBackup();
                }
            }
            else
            {
                LoadFromLegacyBackup();
            }
        }

        private void LoadFromLegacyBackup()
        {
            Current = new GeneralSettings();
            LoadGeneralPartFromLegacy();
            LoadDashboardPart();
        }

        private void LoadGeneralPartFromLegacy()
        {
            Current.DbProvider = Settings.Default.DbProvider;
            Current.SqlAuthConnString = Settings.Default.SqlAuthConnString;
            Current.SqlDataConnString = Settings.Default.SqlDataConnString;
            Current.PostgresDataConnString = Settings.Default.PostgresDataConnString;
            Current.PostgresAuthConnString = Settings.Default.PostgresAuthConnString;
            Current.AppLanguage = Settings.Default.AppLanguage;
            Current.AutoImportEnabled = Settings.Default.AutoImportEnabled;
            Current.ImportIsRelative = Settings.Default.ImportIsRelative;
            Current.ImportFileName = Settings.Default.ImportFileName;
            Current.ImportAbsolutePath = Settings.Default.ImportAbsolutePath;
            Current.ConnectionTimeout = Settings.Default.ConnectionTimeout;
            Current.TrustServerCertificate = Settings.Default.TrustServerCertificate;
            Current.DbServerName = Settings.Default.DbServerName;
            Current.DbHost = Settings.Default.DbHost;
            Current.DbPort = Settings.Default.DbPort;
            Current.DbUser = Settings.Default.DbUser;

            string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
            if (File.Exists(offlineConfig))
                Current.OfflineFolderPath = File.ReadAllText(offlineConfig).Trim();
            else
                Current.OfflineFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData");
        }

        private void LoadDashboardPart()
        {
            if (Current == null) Current = new GeneralSettings();
            Current.ShowDashboardDateFilter = Settings.Default.ShowDashboardDateFilter;
            Current.DashboardDateTickSize = Settings.Default.DashboardDateTickSize;
            Current.DefaultRowLimit = Settings.Default.DefaultRowLimit;

            string categoryRulesPath = "category_rules.json";
            if (File.Exists(categoryRulesPath))
            {
                try
                {
                    string json = File.ReadAllText(categoryRulesPath);
                    Current.CategoryRules = JsonConvert.DeserializeObject<List<CategoryRule>>(json) ?? new List<CategoryRule>();
                }
                catch { }
            }
        }

        public void Save()
        {
            if (Current == null) return;
            string configPath = GetResolvedConfigPath();
            try
            {
                string dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(configPath, json);

                Settings.Default.DbProvider = Current.DbProvider;
                Settings.Default.SqlAuthConnString = Current.SqlAuthConnString;
                Settings.Default.SqlDataConnString = Current.SqlDataConnString;
                Settings.Default.PostgresDataConnString = Current.PostgresDataConnString;
                Settings.Default.PostgresAuthConnString = Current.PostgresAuthConnString;
                Settings.Default.AppLanguage = Current.AppLanguage;
                Settings.Default.AutoImportEnabled = Current.AutoImportEnabled;
                Settings.Default.ImportIsRelative = Current.ImportIsRelative;
                Settings.Default.ImportFileName = Current.ImportFileName;
                Settings.Default.ImportAbsolutePath = Current.ImportAbsolutePath;
                Settings.Default.ShowDashboardDateFilter = Current.ShowDashboardDateFilter;
                Settings.Default.DashboardDateTickSize = Current.DashboardDateTickSize;
                Settings.Default.DefaultRowLimit = Current.DefaultRowLimit;
                Settings.Default.ConnectionTimeout = Current.ConnectionTimeout;
                Settings.Default.TrustServerCertificate = Current.TrustServerCertificate;
                Settings.Default.DbServerName = Current.DbServerName;
                Settings.Default.DbHost = Current.DbHost;
                Settings.Default.DbPort = Current.DbPort;
                Settings.Default.DbUser = Current.DbUser;
                Settings.Default.Save();

                string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
                File.WriteAllText(offlineConfig, Current.OfflineFolderPath);

                string categoryRulesPath = "category_rules.json";
                if (Current.CategoryRules != null)
                {
                    var orderedRules = Current.CategoryRules.OrderByDescending(r => r.Priority).ToList();
                    string rulesJson = JsonConvert.SerializeObject(orderedRules, Formatting.Indented);
                    File.WriteAllText(categoryRulesPath, rulesJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving general config: {ex.Message}");
            }
        }

        public void ExportGeneralConfig(string filePath)
        {
            if (Current == null) return;
            try
            {
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
```

---

### File: DatabaseRetryPolicy.cs

```csharp
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Npgsql;

namespace WPF_LoginForm.Services.Database
{
    public static class DatabaseRetryPolicy
    {
        private const int MaxRetries = 3;
        private const int DelayMilliseconds = 1000;

        public static event Action<string> OnRetryStatus;

        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    attempts++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    if (attempts >= MaxRetries || !IsTransient(ex))
                    {
                        throw;
                    }
                    string msg = $"⚠️ Network unstable. Retrying (Attempt {attempts}/{MaxRetries})...";
                    System.Diagnostics.Debug.WriteLine(msg);
                    OnRetryStatus?.Invoke(msg);
                    await Task.Delay(DelayMilliseconds * attempts);
                }
            }
        }

        public static async Task ExecuteAsync(Func<Task> operation)
        {
            await ExecuteAsync<bool>(async () => { await operation(); return true; });
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is SqlException sqlEx)
            {
                foreach (SqlError err in sqlEx.Errors)
                {
                    switch (err.Number)
                    {
                        case -2: case 53: case 121: case 10054: case 10060: case 40613: return true;
                    }
                }
            }
            if (ex is PostgresException pgEx)
            {
                if (pgEx.SqlState.StartsWith("08") || pgEx.SqlState == "57P03") return true;
            }
            if (ex.InnerException is System.Net.Sockets.SocketException) return true;
            return false;
        }
    }
}
```

---

### File: UserSessionService.cs

```csharp
using System;

namespace WPF_LoginForm.Services
{
    public static class UserSessionService
    {
        private static string _currentRole = "Guest";
        private static string _currentUsername = "";

        public static string CurrentRole => _currentRole;
        public static string CurrentUsername => _currentUsername;
        public static bool IsAdmin => string.Equals(_currentRole, "Admin", StringComparison.OrdinalIgnoreCase);

        public static void SetSession(string username, string role)
        {
            _currentUsername = username;
            if (!string.IsNullOrEmpty(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _currentRole = "Admin";
            }
            else
            {
                _currentRole = role?.Trim() ?? "User";
            }
        }

        public static void Logout()
        {
            _currentRole = "Guest";
            _currentUsername = "";
        }
    }
}
```

---

### File: UIColors.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="exColor1">#060501</Color>
    <Color x:Key="exColor4">#2F2707</Color>
    <Color x:Key="exColor2">#16640D</Color>
    <Color x:Key="exColor3">#21A316</Color>
    <Color x:Key="primaryBackColor1">#4D9939</Color>
    <Color x:Key="primaryBackColor2">#060543</Color>
    <SolidColorBrush x:Key="primaryBackColor2Brush" Color="{StaticResource primaryBackColor2}" />
    <Color x:Key="primaryBackColor3">#215C0E</Color>
    <Color x:Key="primaryBackColor4">#179F15</Color>
    <Color x:Key="secondaryBackColor1">#12A10B</Color>
    <Color x:Key="secondaryBackColor2">#030347</Color>
    <Color x:Key="winBorderColor1">#2C610C</Color>
    <Color x:Key="winBorderColor2">#33730D</Color>
    <Color x:Key="winBorderColor3">#49A956</Color>
    <SolidColorBrush x:Key="color1" Color="#1D348C" />
    <SolidColorBrush x:Key="color2" Color="#2B3F8B" />
    <SolidColorBrush x:Key="color3" Color="#29426B" />
    <SolidColorBrush x:Key="color4" Color="#0C7081" />
    <SolidColorBrush x:Key="color5" Color="#14BB14" />
    <SolidColorBrush x:Key="color6" Color="#FFC047" />
    <SolidColorBrush x:Key="color7" Color="#EF6C96" />
    <SolidColorBrush x:Key="color8" Color="#78A3FC" />
    <SolidColorBrush x:Key="color9" Color="#07F3C0" />
    <SolidColorBrush x:Key="color10" Color="#FBA1AA" />
    <SolidColorBrush x:Key="titleColor1" Color="#E0E1F1" />
    <SolidColorBrush x:Key="titleColor2" Color="#B2ABD4" />
    <SolidColorBrush x:Key="titleColor3" Color="#BCBEE0" />
    <SolidColorBrush x:Key="plainTextColor1" Color="#9497CD" />
    <SolidColorBrush x:Key="plainTextColor2" Color="#7C80C2" />
    <SolidColorBrush x:Key="plainTextColor3" Color="#65B48B" />
    <SolidColorBrush x:Key="panelColor" Color="#18CE26" />
    <SolidColorBrush x:Key="panelOverColor" Color="#0B9F30" />
    <SolidColorBrush x:Key="panelActiveColor" Color="#0B9F72" />
    <SolidColorBrush x:Key="HoverLightBlueBrush" Color="#5E85C9" />
    <SolidColorBrush x:Key="PressedBlueBrush" Color="#3A5F9E" />
    <SolidColorBrush x:Key="ButtonHoverBrush" Color="#20FFFFFF" />
    <SolidColorBrush x:Key="ButtonPressedBrush" Color="#40FFFFFF" />
    <SolidColorBrush x:Key="DangerBrush" Color="#D14575" />
    <SolidColorBrush x:Key="DangerHoverBrush" Color="#D14F59" />
    <SolidColorBrush x:Key="statusAddedColor" Color="#4F977D" />
    <SolidColorBrush x:Key="statusModifiedColor" Color="#9CCF46" />
    <SolidColorBrush x:Key="statusEditableColor" Color="#AEAEE9" />
    <Color x:Key="PrimaryColor">#030347</Color>
    <Color x:Key="SecondaryColor">#FF303060</Color>
    <Color x:Key="AccentColor">#FFF0B400</Color>
    <Color x:Key="exbhColor1">#1A9B38</Color>
    <Color x:Key="exbhColor2">#21A316</Color>
    <LinearGradientBrush x:Key="Brush1" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="{StaticResource SecondaryColor}" Offset="0.0" />
        <GradientStop Color="{StaticResource exbhColor1}" Offset="0.4" />
        <GradientStop Color="{StaticResource winBorderColor1}" Offset="0.6" />
        <GradientStop Color="{StaticResource SecondaryColor}" Offset="1" />
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="Brush2" StartPoint="0.5,0" EndPoint="0.5,1">
        <GradientStop Color="#FF404070" Offset="0.0" />
        <GradientStop Color="{StaticResource SecondaryColor}" Offset="0.8" />
    </LinearGradientBrush>
</ResourceDictionary>
```

---

### File: ButtonStyles.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:fa="http://schemas.fontawesome.io/icons/"
                    xmlns:sharp="http://schemas.awesome.incremented/wpf/xaml/fontawesome.sharp">

    <Style x:Key="menuButton" TargetType="RadioButton">
        <Setter Property="Height" Value="50" />
        <Setter Property="Margin" Value="-5,0,0,5" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource color5}" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RadioButton">
                    <Border Background="{TemplateBinding Background}"
                            BorderThickness="4,0,0,0"
                            BorderBrush="{TemplateBinding BorderBrush}">
                        <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource panelOverColor}" />
                <Setter Property="Foreground" Value="{StaticResource titleColor3}" />
                <Setter Property="BorderBrush" Value="{Binding Path=Tag, RelativeSource={RelativeSource Self}}" />
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Background" Value="{StaticResource panelActiveColor}" />
                <Setter Property="Foreground" Value="{Binding Path=Tag, RelativeSource={RelativeSource Self}}" />
                <Setter Property="BorderBrush" Value="{Binding Path=Tag, RelativeSource={RelativeSource Self}}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="StandardButtonStyle" TargetType="Button">
        <Setter Property="Height" Value="40" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Background" Value="{StaticResource panelColor}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" CornerRadius="5">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource HoverLightBlueBrush}" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
                <Setter Property="Background" Value="{StaticResource PressedBlueBrush}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Opacity" Value="0.5" />
                <Setter Property="Cursor" Value="No" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="menuButtonIcon" TargetType="{x:Type fa:ImageAwesome}">
        <Setter Property="Foreground" Value="{Binding Path=Foreground, RelativeSource={RelativeSource AncestorType=RadioButton}}" />
        <Setter Property="Width" Value="22" />
        <Setter Property="Height" Value="22" />
        <Setter Property="Margin" Value="35,0,20,0" />
    </Style>

    <Style x:Key="menuButtonText" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{Binding Path=Foreground, RelativeSource={RelativeSource AncestorType=RadioButton}}" />
        <Setter Property="FontFamily" Value="Montserrat" />
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="FontSize" Value="15.5" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>

    <Style x:Key="WindowControlButtonStyle" TargetType="Button">
        <Setter Property="Width" Value="30" />
        <Setter Property="Height" Value="30" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource ButtonHoverBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="WindowCloseButtonStyle" TargetType="Button" BasedOn="{StaticResource WindowControlButtonStyle}">
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource DangerBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="DashIcon" TargetType="{x:Type fa:ImageAwesome}">
        <Setter Property="Foreground" Value="{StaticResource titleColor2}" />
        <Setter Property="Width" Value="20" />
        <Setter Property="Height" Value="20" />
        <Setter Property="Margin" Value="10,0" />
    </Style>
</ResourceDictionary>
```

---

### File: ThemeColors.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="AppBackgroundBrush" Color="#050348" />
    <SolidColorBrush x:Key="PanelBackgroundBrush" Color="#1E177A" />
    <SolidColorBrush x:Key="InputBackgroundBrush" Color="#1F2125" />
    <SolidColorBrush x:Key="PrimaryAccentBrush" Color="#1C278C" />
    <SolidColorBrush x:Key="PrimaryHoverBrush" Color="#1a21d9" />
    <SolidColorBrush x:Key="SecondaryAccentBrush" Color="#09AE25" />
    <SolidColorBrush x:Key="HighlightBrush" Color="#8096D2F3" />
    <SolidColorBrush x:Key="SuccessBrush" Color="#093A0E" />
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="#E6E7ED" />
    <SolidColorBrush x:Key="TextMutedBrush" Color="#DFE2F9" />
    <SolidColorBrush x:Key="RowBackgroundBrush" Color="#383839" />
    <SolidColorBrush x:Key="AltRowBackgroundBrush" Color="#464648" />
    <SolidColorBrush x:Key="GridLineBrush" Color="#303030" />
    <SolidColorBrush x:Key="TitleBarDarkBlueBrush" Color="#001B3D" />
    <SolidColorBrush x:Key="ScrollBarThumbBlueBrush" Color="#0078D7" />
    <SolidColorBrush x:Key="ScrollBarTrackBrush" Color="#1A1A1A" />
    <SolidColorBrush x:Key="TitleBarDarkBlue" Color="#00008B" />
    <SolidColorBrush x:Key="ScrollBarBlueBrush" Color="#007ACC" />
    <SolidColorBrush x:Key="TextWhiteBrush" Color="#FFFFFF" />
</ResourceDictionary>
```

---

### File: DarkTheme.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="Theme.Background" Color="#02014B" />
    <SolidColorBrush x:Key="Theme.TitleBar" Color="#0A0F8F" />
    <SolidColorBrush x:Key="Theme.Panel" Color="#0D1263" />
    <SolidColorBrush x:Key="Theme.Text.Primary" Color="#BFD6BF" />
    <SolidColorBrush x:Key="Theme.Text.Secondary" Color="#BDC3C7" />
    <SolidColorBrush x:Key="Theme.Grid.Header" Color="#050833" />
    <SolidColorBrush x:Key="Theme.Grid.Row" Color="#0A0E3D" />
    <SolidColorBrush x:Key="Theme.Grid.Lines" Color="#1C2255" />
    <SolidColorBrush x:Key="Theme.Accent" Color="#7C7BD8" />
    <SolidColorBrush x:Key="Theme.Footer" Color="#49497A" />
    <SolidColorBrush x:Key="Theme.Combo.Background" Color="#1E2A47" />
    <SolidColorBrush x:Key="Theme.Combo.Foreground" Color="#FFFFFF" />
</ResourceDictionary>
```

---

### Dependency Graph Summary

```
DatarepView.xaml
  └── DatarepView.xaml.cs (code-behind)
        └── DatarepViewModel.cs (DataContext)
              ├── ViewModelBase.cs (INotifyPropertyChanged base)
              ├── ViewModelCommand.cs (ICommand implementation)
              ├── ILogger.cs (interface)
              ├── IDialogService.cs (interface)
              ├── IDataRepository.cs (interface)
              ├── DataImportExportHelper.cs (static utility)
              ├── GeneralSettingsManager.cs (singleton settings)
              │     └── GeneralSettings.cs (model)
              ├── UserSessionService.cs (static auth state)
              ├── DatabaseRetryPolicy.cs (retry logic)
              ├── NewRowData.cs (model)
              └── ImportSettings.cs (model)
              
DatarepView.xaml (XAML resources)
  ├── BooleanToVisibilityConverter.cs
  ├── BooleanInverterConverter.cs
  ├── IsRowEditableConverter.cs
  ├── IsItemInCollectionConverter.cs
  ├── IsNullOrEmptyConverter.cs
  └── DateTimeCorrectionConverter.cs
  
App.xaml (merged dictionaries)
  ├── UIColors.xaml (colors, brushes)
  ├── ButtonStyles.xaml (StandardButtonStyle, menuButton, etc.)
  ├── ThemeColors.xaml (theme brushes)
  └── DarkTheme.xaml (Theme.Grid.*)
```
