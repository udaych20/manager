#!/bin/sh

# Default to not following logs
FOLLOW_LOGS=""

# Check if the first argument is 'follow', set FOLLOW_LOGS to '-f' if true
if [ "$1" = "f" ]; then
  FOLLOW_LOGS="-f"
fi

# Get the pod name
POD_NAME=$(kubectl get pods -n sdmd --selector=app=systems-manager -o jsonpath='{.items[0].metadata.name}')

# Check if POD_NAME is empty
if [ -z "$POD_NAME" ]; then
  echo "No pods found for 'systems-manager'."
  exit 1
fi

# Output logs
kubectl logs $FOLLOW_LOGS $POD_NAME -n sdmd
