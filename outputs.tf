output "application_insights_instrumentation_key" {
  value = azurerm_application_insights.application_insights.instrumentation_key
  description = "Deployed Application Insights instrumentation key"
  sensitive = true
}

output "application_insights_connection_string" {
  value = azurerm_application_insights.application_insights.connection_string
  description = "Deployed Application Insights connection string"
  sensitive = true
}

output "application_insights_app_id" {
  value = azurerm_application_insights.application_insights.app_id
  description = "Deployed Application Insights app id"
}

output "cosmosdb_connection_string" {
  value = azurerm_cosmosdb_account.dbaccount.primary_sql_connection_string
  description = "Deployed Cosmosdb connection string"
  sensitive = true
}

output "function_app_name" {
  value = azurerm_windows_function_app.function_app.name
  description = "Deployed function app name"
}

output "function_app_default_hostname" {
  value = azurerm_windows_function_app.function_app.default_hostname
  description = "Deployed function app hostname"
}