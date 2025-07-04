# WotCI Setup Makefile
# This Makefile sets up the certificate infrastructure for WotCI

.PHONY: all clean setup-certs setup-manifests build run help

# Default target
all: setup-certs setup-manifests

# Build the certificate utility
build-certutil:
	@echo "Building SolarSharp.CertUtil..."
	dotnet build ../SolarSharp.CertUtil

# Generate root CA certificate
setup-ca: build-certutil
	@echo "Generating root CA certificate..."
	@mkdir -p certs
	dotnet run --project ../SolarSharp.CertUtil -- generate-ca --name "Broken Build Entertainment Root CA" --output certs

# Issue partner certificates from the CA
setup-partner-certs: setup-ca
	@echo "Issuing partner certificates..."
	dotnet run --project ../SolarSharp.CertUtil -- issue-cert \
		--ca-cert certs/root-ca.crt \
		--ca-key certs/root-ca.key \
		--subject "CN=/plugins/deadlock-digital, O=Deadlock Digital, C=US" \
		--constraint "/plugins/deadlock-digital" \
		--output certs/deadlock-digital
	
	dotnet run --project ../SolarSharp.CertUtil -- issue-cert \
		--ca-cert certs/root-ca.crt \
		--ca-key certs/root-ca.key \
		--subject "CN=/plugins/segfault-studios, O=Segfault Studios, C=US" \
		--constraint "/plugins/segfault-studios" \
		--output certs/segfault-studios

# Set up all certificates
setup-certs: setup-partner-certs
	@echo "✓ Certificate setup complete"

# Create plugin manifests
setup-manifests: setup-certs
	@echo "Creating plugin manifests..."
	@echo '{\n  "version": "1.0",\n  "name": "Deadlock Digital Plugin Suite",\n  "author": "Deadlock Digital",\n  "policy": {\n    "allowedModules": ["basic", "string", "table", "math", "io", "os"],\n    "capabilities": ["FileRead", "FileWrite", "DirectoryOperations"],\n    "timeout": 30,\n    "memoryLimit": 100,\n    "defaultFileAccess": "sandboxedreadwrite",\n    "defaultDirectoryAccess": "listandcreatefiles",\n    "filePermissions": {\n      "/game/assets/**": "read",\n      "/game/saves/*.sav": "sandboxedreadwrite",\n      "/game/logs/*.log": "readwrite"\n    }\n  },\n  "files": {}\n}' > plugins/deadlock-digital/manifest.json
	
	@echo '{\n  "version": "1.0",\n  "name": "Segfault Studios Enhancement Pack",\n  "author": "Segfault Studios",\n  "policy": {\n    "allowedModules": ["basic", "string", "table", "math", "io", "os"],\n    "capabilities": ["FileRead", "FileWrite", "DirectoryOperations"],\n    "timeout": 30,\n    "memoryLimit": 100,\n    "defaultFileAccess": "sandboxedreadwrite",\n    "defaultDirectoryAccess": "listandcreatefiles",\n    "filePermissions": {\n      "/game/assets/**": "read",\n      "/game/saves/*.sav": "sandboxedreadwrite",\n      "/game/logs/*.log": "readwrite"\n    }\n  },\n  "files": {}\n}' > plugins/segfault-studios/manifest.json
	
	@echo "Signing manifests..."
	dotnet run --project ../SolarSharp.CertUtil -- sign-manifest \
		--manifest plugins/deadlock-digital/manifest.json \
		--cert certs/deadlock-digital.crt \
		--key certs/deadlock-digital.key
	
	dotnet run --project ../SolarSharp.CertUtil -- sign-manifest \
		--manifest plugins/segfault-studios/manifest.json \
		--cert certs/segfault-studios.crt \
		--key certs/segfault-studios.key
	
	@echo "✓ Manifest setup complete"

# Build the WotCI game
build: 
	@echo "Building WotCI..."
	dotnet build

# Run the game
run: build
	@echo "Starting WotCI..."
	dotnet run

# Clean up generated files
clean:
	@echo "Cleaning up certificates and manifests..."
	rm -rf certs/
	rm -f plugins/deadlock-digital/manifest.json
	rm -f plugins/segfault-studios/manifest.json
	@echo "✓ Cleanup complete"

# Show help
help:
	@echo "WotCI Makefile Commands:"
	@echo ""
	@echo "  all              - Set up certificates and manifests (default)"
	@echo "  setup-certs      - Generate all certificates"
	@echo "  setup-manifests  - Create and sign plugin manifests"
	@echo "  build            - Build the WotCI game"
	@echo "  run              - Build and run the WotCI game"
	@echo "  clean            - Remove generated certificates and manifests"
	@echo "  help             - Show this help message"
	@echo ""
	@echo "Certificate Management:"
	@echo "  setup-ca             - Generate root CA only"
	@echo "  setup-partner-certs  - Generate partner certificates"
	@echo ""
	@echo "Individual certificate commands can be run with:"
	@echo "  dotnet run --project ../SolarSharp.CertUtil -- <command> [options]"
	@echo ""
	@echo "For certificate utility help:"
	@echo "  dotnet run --project ../SolarSharp.CertUtil -- --help"