using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DocumentFlowApp.WPF.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IDocumentService _documentService;
        private ObservableCollection<Document> _documents;
        private Document _selectedDocument;

        public MainViewModel(IDocumentService documentService)
        {
            _documentService = documentService;
            _documents = new ObservableCollection<Document>();

            //Команды
            LoadDocumentsCommand = new RelayCommand(async () => await LoadDocumentsAsync());
            CreateDocumentCommand = new RelayCommand(async () => await CreateDocumentAsync());
            DeleteDocumentCommand = new RelayCommand(async () => await DeleteDocumentAsync(),
                                                   () => SelectedDocument != null);

            //Загружаем документы при старте
            LoadDocumentsCommand.Execute(null);
        }

        public ObservableCollection<Document> Documents
        {
            get => _documents;
            set => SetProperty(ref _documents, value);
        }

        public Document SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                SetProperty(ref _selectedDocument, value);
                (DeleteDocumentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        //Команды
        public ICommand LoadDocumentsCommand { get; }
        public ICommand CreateDocumentCommand { get; }
        public ICommand DeleteDocumentCommand { get; }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var documents = await _documentService.GetAllDocumentsAsync();
                Documents = new ObservableCollection<Document>(documents);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки документов: {ex.Message}", "Ошибка");
            }
        }

        private async Task CreateDocumentAsync()
        {
            try
            {
                var document = await _documentService.CreateDocumentAsync(
                    "Новый документ",
                    "Описание нового документа",
                    Core.Enums.DocumentType.Other);

                Documents.Insert(0, document);
                System.Windows.MessageBox.Show("Документ создан успешно!", "Успех");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка создания документа: {ex.Message}", "Ошибка");
            }
        }
        private async Task DeleteDocumentAsync()
        {
            if (SelectedDocument == null) return;

            try
            {
                await _documentService.DeleteDocumentAsync(SelectedDocument.Id);
                Documents.Remove(SelectedDocument);
                System.Windows.MessageBox.Show("Документ удален успешно!", "Успех");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка удаления документа: {ex.Message}", "Ошибка");
            }
        }
    }
}
