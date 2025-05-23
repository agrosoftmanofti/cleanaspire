﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CleanAspire.Domain.Common;

public abstract class BaseAuditableSoftDeleteEntity : BaseAuditableEntity, ISoftDelete
{
    public DateTime? Deleted { get; set; }
    public string? DeletedBy { get; set; }
}