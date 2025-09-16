#!/bin/bash

# The URL to fetch the current service details
FETCH_URL="http://10.43.229.194:8080/services"

# Fetch the current service details
response=$(curl -L "$FETCH_URL")

# Extract the current status, category, and other details (assuming the first item in the array)
id=$(echo $response | jq -r '.[0].id')
serviceName=$(echo $response | jq -r '.[0].serviceName')
# Note: Removed the line extracting the current category since it will be replaced by user input
state=$(echo $response | jq -r '.[0].state')
priority=$(echo $response | jq -r '.[0].priority')
mode=$(echo $response | jq -r '.[0].mode')

# Ask the user for the new category
current_category=$(echo $response | jq -r '.[0].category')
echo "Current category is: $current_category"
read -p "Enter new category: " new_category

# Ask the user for the new status
current_status=$(echo $response | jq -r '.[0].status')
echo "Current status is: $current_status"
read -p "Enter new status: " new_status

# Prepare the updated JSON data using fetched details and user inputs
updated_json=$(jq -n \
                  --arg id "$id" \
                  --arg serviceName "$serviceName" \
                  --arg category "$new_category" \
                  --arg state "$state" \
                  --arg priority "$priority" \
                  --arg status "$new_status" \
                  --arg mode "$mode" \
                  '{
                    id: ($id | tonumber),
                    ServiceName: $serviceName,
                    Category: $category,
                    State: $state,
                    Priority: ($priority | tonumber),
                    Status: $status,
                    Mode: $mode
                  }')

# The URL to update the service details (assuming ID is part of the URL)
UPDATE_URL="http://10.43.229.194:8080/services/$id"

# Update the service details
curl -L -X PUT "$UPDATE_URL" \
-H "Content-Type: application/json" \
-d "$updated_json"

echo "Service updated."

# Wait a moment to ensure the update has been processed
sleep 2

# Fetch and display the updated service details
echo "Fetching updated service details..."
updated_response=$(curl -L "$FETCH_URL")
echo $updated_response | jq

