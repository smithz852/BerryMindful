using System.ComponentModel.DataAnnotations;
using BerryMindful.Data.Entities;

namespace BerryMindful.Services.DTOs;

public record PantryItemDraftDto(
    [Required, MaxLength(256)] string Name,
    ItemCategory Category,
    [Range(0, 3650)] int EstimatedExpiryDays);

public record ReceiptScanResultDto(
    string? StoreNameRaw,
    DateTime PurchasedAt,
    string? ImageUrl,
    string? RawOcrText,
    IReadOnlyList<PantryItemDraftDto> Items);

public record ConfirmReceiptRequest(
    string? StoreNameRaw,
    DateTime PurchasedAt,
    string? ImageUrl,
    string? RawOcrText,
    [Required, MinLength(1)] List<PantryItemDraftDto> Items);

public record ReceiptSummaryDto(
    Guid Id,
    string? StoreNameRaw,
    DateTime PurchasedAt,
    DateTime CreatedAt,
    int ItemCount);
