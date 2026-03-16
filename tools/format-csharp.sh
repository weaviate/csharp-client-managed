#!/bin/bash
# Wrapper script for CSharpier to work with pre-commit
# This script formats the provided files using CSharpier

# Exit immediately if a command exits with a non-zero status
set -e

# Check if files were provided
if [ $# -eq 0 ]; then
    echo "No files provided to format"
    exit 0
fi

# Format each file passed to the script
for file in "$@"; do
    dotnet csharpier format "$file"
done
