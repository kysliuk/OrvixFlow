using System;
using System.Collections.Generic;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Models;

public record EmailDraftResult(
    string DraftBody,
    bool IsInsufficientContext,
    IReadOnlyList<KnowledgeImageRef> RelevantImages,
    float DraftConfidence = 1.0f
);
