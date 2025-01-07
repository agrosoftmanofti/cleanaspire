﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MudBlazor;

namespace CleanAspire.ClientApp.Services.Navigation;

public static class NavbarMenu
{
    public static List<MenuItem> Default = new List<MenuItem>
{
    new MenuItem
    {
        Label = "Application",
        StartIcon = Icons.Material.Filled.AppRegistration,
        EndIcon = Icons.Material.Filled.KeyboardArrowDown,
        SubItems = new List<MenuItem>
        {
            new MenuItem
            {
                Label = "Products",
                SubItems = new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "All Products",
                        Href = "/products/index",
                        Status = PageStatus.New,
                        Description = "View all available products in our inventory."
                    },
                    new MenuItem
                    {
                        Label = "Stock Inquiry",
                        Href = "/stocks/index",
                        Status = PageStatus.Completed,
                        Description = "Check product stock levels."
                    },
                    new MenuItem
                    {
                        Label = "Best Sellers",
                        Href = "",
                        Status = PageStatus.Completed,
                        Description = "See our top-selling products."
                    }
                }
            },
            new MenuItem
            {
                Label = "Orders",
                SubItems = new List<MenuItem>
                {
                    new MenuItem
                    {
                        Label = "Order Overview",
                        Href = "/orders/overview",
                        Status = PageStatus.ComingSoon,
                        Description = "Overview of all customer orders."
                    },
                    new MenuItem
                    {
                        Label = "Shipment Details",
                        Href = "/orders/shipments",
                        Status = PageStatus.ComingSoon,
                        Description = "Track the shipment details of orders."
                    }
                }
            }
        }
    },
    new MenuItem
    {
        Label = "Reports",
        StartIcon = Icons.Material.Filled.Dashboard,
        EndIcon = Icons.Material.Filled.KeyboardArrowDown,
        SubItems = new List<MenuItem>
        {
            new MenuItem
            {
                Label = "Overview",
                Href = "/reports/overview",
                Status = PageStatus.Completed,
                Description = "View an overview of all reports."
            },
            new MenuItem
            {
                Label = "Statistics",
                Href = "/reports/statistics",
                Status = PageStatus.New,
                Description = "Analyze detailed statistics for performance tracking."
            },
            new MenuItem
            {
                Label = "Activity Log",
                Href = "/reports/activitylog",
                Status = PageStatus.Completed,
                Description = "View the activity log for user actions."
            }
        }
    },
    new MenuItem
    {
        Label = "Help",
        StartIcon = Icons.Material.Filled.Help,
        EndIcon = Icons.Material.Filled.KeyboardArrowDown,
        SubItems = new List<MenuItem>
        {
            new MenuItem
            {
                Label = "Documentation",
                Href = "/help/documentation",
                Status = PageStatus.Completed,
                Description = "Access the user and developer documentation."
            },
            new MenuItem
            {
                Label = "GitHub",
                Href = "https://github.com/neozhu/cleanaspire/",
                Status = PageStatus.Completed,
                Description = "Visit our GitHub repository."
            }
        }
    }
};
}
