﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CleanAspire.Application.Common.Models;
using CleanAspire.Application.Features.Products.Commands;
using CleanAspire.Application.Features.Products.DTOs;
using CleanAspire.Application.Features.Products.Queries;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CleanAspire.Api.Endpoints;

public class ProductEndpointRegistrar : IEndpointRegistrar
{
    public void RegisterRoutes(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/products").WithTags("products").AllowAnonymous();

        // Get all products
        group.MapGet("/", async ([FromServices] IMediator mediator) =>
        {
            var query = new GetAllProductsQuery();
            return await mediator.Send(query);
        })
        .Produces<IEnumerable<ProductDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get all products")
        .WithDescription("Returns a list of all products in the system.");

        // Get product by ID
        group.MapGet("/{id}", (IMediator mediator, [FromRoute] string id) => mediator.Send(new GetProductByIdQuery(id)))
        .Produces<ProductDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get product by ID")
        .WithDescription("Returns the details of a specific product by its unique ID.");

        // Create a new product
        group.MapPost("/", ([FromServices] IMediator mediator, [FromBody] CreateProductCommand command) => mediator.Send(command))
             .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create a new product")
        .WithDescription("Creates a new product with the provided details.");

        // Update an existing product
        group.MapPut("/", ([FromServices] IMediator mediator, [FromBody] UpdateProductCommand command) => mediator.Send(command))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an existing product")
        .WithDescription("Updates the details of an existing product.");

        // Delete products by IDs
        group.MapDelete("/", ([FromServices] IMediator mediator, [FromBody] DeleteProductCommand command) => mediator.Send(command))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Delete products by IDs")
        .WithDescription("Deletes one or more products by their unique IDs.");

        // Get products with pagination and filtering
        group.MapPost("/pagination", ([FromServices] IMediator mediator, [FromBody] ProductsWithPaginationQuery query) => mediator.Send(query))
        .Produces<PaginatedResult<ProductDto>>(StatusCodes.Status200OK)
        .WithSummary("Get products with pagination")
        .WithDescription("Returns a paginated list of products based on search keywords, page size, and sorting options.");
    }
}

