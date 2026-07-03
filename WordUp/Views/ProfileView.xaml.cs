using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WordUp.ViewModels;

namespace WordUp.Views;

public partial class ProfileView : UserControl
{
    private MainViewModel? viewModel;
    private bool isUpdatingNoteEditor;

    public ProfileView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as MainViewModel);
        UpdateNoteEditor();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as MainViewModel);
    }

    private void AttachViewModel(MainViewModel? nextViewModel)
    {
        if (ReferenceEquals(viewModel, nextViewModel))
        {
            return;
        }

        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        viewModel = nextViewModel;

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateNoteEditor();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ProfileNote))
        {
            UpdateNoteEditor();
        }
    }

    private void UpdateNoteEditor()
    {
        if (viewModel is null)
        {
            return;
        }

        var currentText = GetNoteEditorText();
        if (currentText == viewModel.ProfileNote)
        {
            return;
        }

        isUpdatingNoteEditor = true;
        NoteEditor.Document.Blocks.Clear();
        NoteEditor.Document.Blocks.Add(new Paragraph(new Run(viewModel.ProfileNote)));
        isUpdatingNoteEditor = false;
    }

    private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isUpdatingNoteEditor || viewModel is null)
        {
            return;
        }

        viewModel.ProfileNote = GetNoteEditorText();
    }

    private string GetNoteEditorText()
    {
        return new TextRange(NoteEditor.Document.ContentStart, NoteEditor.Document.ContentEnd)
            .Text
            .TrimEnd('\r', '\n');
    }
}
