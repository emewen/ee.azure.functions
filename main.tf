terraform {
  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
      version = "4.5.0"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = "42b21f74-d14e-4b43-9b45-473634e76e7f"
}

resource "random_integer" "ri" {
  min = 10000
  max = 99999
}

resource "azurerm_resource_group" "resourcegroup" {
  name     = "${var.rgName}-${random_integer.ri.result}"
  location = var.locationName
}

resource "azurerm_cosmosdb_account" "dbaccount" {
  name                = "${var.cosmosName}-${random_integer.ri.result}"
  location            = azurerm_resource_group.resourcegroup.location
  resource_group_name = azurerm_resource_group.resourcegroup.name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  automatic_failover_enabled = false

  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = var.locationName
    failover_priority = 0
  }
}

resource "azurerm_cosmosdb_sql_database" "db" {
  name                = "stock-price-database-${random_integer.ri.result}"
  resource_group_name = azurerm_resource_group.resourcegroup.name
  account_name        = azurerm_cosmosdb_account.dbaccount.name
}

resource "azurerm_cosmosdb_sql_container" "container" {
  name                  = "stock-price-container-${random_integer.ri.result}"
  resource_group_name   = azurerm_resource_group.resourcegroup.name
  account_name          = azurerm_cosmosdb_account.dbaccount.name
  database_name         = azurerm_cosmosdb_sql_database.db.name
  partition_key_paths   = ["/id"]
  partition_key_version = 1
  throughput            = 400

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }

    included_path {
      path = "/included/?"
    }

    excluded_path {
      path = "/excluded/?"
    }
  }

  unique_key {
    paths = ["/definition/id"]
  }
}

resource "azurerm_servicebus_namespace" "sbns" {
  name                = "ericewentenant-stock-price-namespace-${random_integer.ri.result}"
  location            = azurerm_resource_group.resourcegroup.location
  resource_group_name = azurerm_resource_group.resourcegroup.name
  sku                 = "Basic"

  tags = {
    source = "terraform"
  }
}

resource "azurerm_servicebus_queue" "sbq" {
  name         = "ericewentenant-stock-action-queue-${random_integer.ri.result}"
  namespace_id = azurerm_servicebus_namespace.sbns.id

  partitioning_enabled = false
}

resource "azurerm_storage_account" "storage_account" {
  name = "eetstockpricestorage"
  resource_group_name = azurerm_resource_group.resourcegroup.name
  location = azurerm_resource_group.resourcegroup.location
  account_tier = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_application_insights" "application_insights" {
  name                = "stock-price-application-insights-${random_integer.ri.result}"
  location            = azurerm_resource_group.resourcegroup.location
  resource_group_name = azurerm_resource_group.resourcegroup.name
  application_type    = "web"
}

resource "azurerm_service_plan" "app_service_plan" {
  name                = "stock-price-app-service-plan-${random_integer.ri.result}"
  resource_group_name = azurerm_resource_group.resourcegroup.name
  location            = azurerm_resource_group.resourcegroup.location
  os_type             = "Windows"
  sku_name            = "Y1"
}

resource "azurerm_windows_function_app" "function_app" {
  name                       = "stock-price-function-app-${random_integer.ri.result}"
  resource_group_name        = azurerm_resource_group.resourcegroup.name
  location                   = azurerm_resource_group.resourcegroup.location
  service_plan_id        = azurerm_service_plan.app_service_plan.id
  app_settings = {
    "WEBSITE_RUN_FROM_PACKAGE" = "",
    "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated",
    "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.application_insights.instrumentation_key,
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.application_insights.connection_string,
    "CosmosDBConnection": azurerm_cosmosdb_account.dbaccount.primary_sql_connection_string,
    "CosmosDb": azurerm_cosmosdb_sql_database.db.name,
    "CosmosContainerIn": azurerm_cosmosdb_sql_container.container.name,
    "CosmosContainerOut": azurerm_cosmosdb_sql_container.container.name,
    "StockLogicAppEndpoint": "https://prod-01.northcentralus.logic.azure.com:443/workflows/2d3bfe5bc5284d9fa53c167ae078adb5/triggers/When_a_HTTP_request_is_received/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2FWhen_a_HTTP_request_is_received%2Frun&sv=1.0&sig=0uR2IOp_wImLNC32DS0YRRmetqdAK3l_OotJ1lfSHpo"
  }
  site_config {
    use_32_bit_worker = false
  }
  storage_account_name       = azurerm_storage_account.storage_account.name
  storage_account_access_key = azurerm_storage_account.storage_account.primary_access_key

  lifecycle {
    ignore_changes = [
      app_settings["WEBSITE_RUN_FROM_PACKAGE"],
    ]
  }
}