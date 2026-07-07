using System.ComponentModel.DataAnnotations;
using BerryMindful.Data.Entities;

namespace BerryMindful.Services.DTOs;

public record PantryItemDto(
    Guid Id,
    string Name,
    ItemCategory Category,
    DateTime PurchasedAt,
    int EstimatedExpiryDays,
    DateTime ExpiresAt,
    PantryItemStatus Status,
    Guid? ReceiptId);

public record AddPantryItemRequest(
    [Required, MaxLength(256)] string Name,
    ItemCategory Category,
    DateTime? PurchasedAt,
    [Range(0, 3650)] int EstimatedExpiryDays);

public record UpdateStatusRequest(PantryItemStatus Status);
