using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Models;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class AttachmentsController : ControllerBase
{
    private const string TransactionObjectType = "transactions";
    private const string LoanObjectType = "loans";
    private const string InsurancePolicyObjectType = "insurance_policies";

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly AttachmentStorageService _storageService;
    private readonly IAuditLogger _auditLogger;

    public AttachmentsController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        RbacService rbacService,
        AttachmentStorageService storageService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _rbacService = rbacService;
        _storageService = storageService;
        _auditLogger = auditLogger;
    }

    [HttpPost("transactions/{transactionId:guid}/attachments")]
    public async Task<ActionResult<AttachmentResponse>> UploadTransactionAttachment(Guid transactionId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var transaction = await _dbContext.Transactions.FirstOrDefaultAsync(tx => tx.Id == transactionId, cancellationToken);
        if (transaction == null)
        {
            return NotFound("Transaction not found.");
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, transaction.EntityId, PermissionAction.Attachments);

        var stored = await _storageService.SaveAsync(_dbContext.TenantId!.Value, TransactionObjectType, transaction.Id, file, cancellationToken);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            LinkedObjectType = TransactionObjectType,
            LinkedObjectId = transaction.Id,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StoragePath,
            UploadedBy = _currentUser.UserId.Value,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync("attachment.uploaded", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Attachment", attachment.Id);

        return CreatedAtAction(nameof(DownloadAttachment), new { attachmentId = attachment.Id }, new AttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAtUtc));
    }

    [HttpPost("loans/{loanId:guid}/attachments")]
    public async Task<ActionResult<AttachmentResponse>> UploadLoanAttachment(Guid loanId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan == null)
        {
            return NotFound("Loan not found.");
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, loan.EntityId, PermissionAction.Attachments);

        var stored = await _storageService.SaveAsync(_dbContext.TenantId!.Value, LoanObjectType, loan.Id, file, cancellationToken);
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            LinkedObjectType = LoanObjectType,
            LinkedObjectId = loan.Id,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StoragePath,
            UploadedBy = _currentUser.UserId.Value,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync("attachment.uploaded", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Attachment", attachment.Id);

        return CreatedAtAction(nameof(DownloadAttachment), new { attachmentId = attachment.Id }, new AttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAtUtc));
    }

    [HttpPost("insurance-policies/{policyId:guid}/attachments")]
    public async Task<ActionResult<AttachmentResponse>> UploadPolicyAttachment(Guid policyId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var policy = await _dbContext.InsurancePolicies.FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken);
        if (policy == null)
        {
            return NotFound("Policy not found.");
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, policy.EntityId, PermissionAction.Attachments);

        var stored = await _storageService.SaveAsync(_dbContext.TenantId!.Value, InsurancePolicyObjectType, policy.Id, file, cancellationToken);
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            LinkedObjectType = InsurancePolicyObjectType,
            LinkedObjectId = policy.Id,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StoragePath,
            UploadedBy = _currentUser.UserId.Value,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync("attachment.uploaded", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Attachment", attachment.Id);

        return CreatedAtAction(nameof(DownloadAttachment), new { attachmentId = attachment.Id }, new AttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAtUtc));
    }

    [HttpGet("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var attachment = await _dbContext.Attachments.FirstOrDefaultAsync(att => att.Id == attachmentId, cancellationToken);
        if (attachment == null)
        {
            return NotFound("Attachment not found.");
        }

        if (attachment.LinkedObjectType == TransactionObjectType)
        {
            var transaction = await _dbContext.Transactions.FirstOrDefaultAsync(tx => tx.Id == attachment.LinkedObjectId, cancellationToken);
            if (transaction == null)
            {
                return NotFound("Linked transaction not found.");
            }

            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, transaction.EntityId, PermissionAction.View);
        }
        else if (attachment.LinkedObjectType == LoanObjectType)
        {
            var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == attachment.LinkedObjectId, cancellationToken);
            if (loan == null)
            {
                return NotFound("Linked loan not found.");
            }

            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, loan.EntityId, PermissionAction.View);
        }
        else if (attachment.LinkedObjectType == InsurancePolicyObjectType)
        {
            var policy = await _dbContext.InsurancePolicies.FirstOrDefaultAsync(p => p.Id == attachment.LinkedObjectId, cancellationToken);
            if (policy == null)
            {
                return NotFound("Linked policy not found.");
            }

            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, policy.EntityId, PermissionAction.View);
        }

        if (!System.IO.File.Exists(attachment.StoragePath))
        {
            return NotFound("File not found.");
        }

        await _auditLogger.LogAsync("attachment.downloaded", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Attachment", attachment.Id);
        return PhysicalFile(attachment.StoragePath, attachment.ContentType, attachment.FileName);
    }
}
