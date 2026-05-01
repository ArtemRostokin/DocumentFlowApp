using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Core.Interfaces;

public interface IDocumentFieldExtractor
{
    DocumentFieldExtractionResult Extract(DocumentType documentType, string? extractedText, string? fileName = null);
}
