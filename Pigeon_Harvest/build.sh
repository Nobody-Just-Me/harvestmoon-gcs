#!/bin/bash

# Pigeon Uno Build Script
# Bash script for building and testing the application on Linux/macOS

set -e  # Exit on any error

# Default values
CONFIGURATION="Debug"
RUNTIME=""
CLEAN=false
TEST=false
PACKAGE=false
PUBLISH=false
COVERAGE=false
VERBOSE=false

# Configuration
SOLUTION_PATH="HarvestmoonGCS.sln"
MAIN_PROJECT="HarvestmoonGCS/HarvestmoonGCS/HarvestmoonGCS.csproj"
TEST_PROJECT="HarvestmoonGCS.Tests/HarvestmoonGCS.Tests.csproj"
OUTPUT_DIR="bin/$CONFIGURATION"
PUBLISH_DIR="publish"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
print_status() {
    echo -e "${BLUE}🔧 $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️ $1${NC}"
}

execute_command() {
    local cmd="$1"
    local desc="$2"
    
    print_status "$desc"
    
    if [ "$VERBOSE" = true ]; then
        echo -e "${BLUE}Executing: $cmd${NC}"
    fi
    
    if ! eval "$cmd"; then
        print_error "$desc failed"
        exit 1
    fi
}

show_help() {
    echo "Pigeon Uno Build Script"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -c, --configuration CONF   Build configuration (Debug|Release) [default: Debug]"
    echo "  -r, --runtime RUNTIME      Target runtime (linux-x64|osx-x64|browser-wasm)"
    echo "  --clean                     Clean before build"
    echo "  --test                      Run tests"
    echo "  --coverage                  Generate code coverage report"
    echo "  --publish                   Publish application"
    echo "  --package                   Create distribution packages"
    echo "  -v, --verbose               Verbose output"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 --clean --test --coverage"
    echo "  $0 -c Release --publish -r linux-x64"
    echo "  $0 --configuration Release --package"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -r|--runtime)
            RUNTIME="$2"
            shift 2
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        --test)
            TEST=true
            shift
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --publish)
            PUBLISH=true
            shift
            ;;
        --package)
            PACKAGE=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Validate configuration
if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
    print_error "Invalid configuration: $CONFIGURATION. Must be Debug or Release."
    exit 1
fi

# Main build process
echo -e "${BLUE}🚀 Pigeon Uno Build Script${NC}"
echo -e "${BLUE}Configuration: $CONFIGURATION${NC}"
if [ -n "$RUNTIME" ]; then
    echo -e "${BLUE}Runtime: $RUNTIME${NC}"
fi
echo ""

# Check prerequisites
print_status "Checking prerequisites..."

if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found. Please install .NET 10 SDK."
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
print_success ".NET SDK version: $DOTNET_VERSION"

# Clean if requested
if [ "$CLEAN" = true ]; then
    print_warning "Cleaning solution..."
    execute_command "dotnet clean $SOLUTION_PATH --configuration $CONFIGURATION" "Clean solution"
    
    if [ -d "$OUTPUT_DIR" ]; then
        rm -rf "$OUTPUT_DIR"
        print_success "Removed output directory: $OUTPUT_DIR"
    fi
    
    if [ -d "$PUBLISH_DIR" ]; then
        rm -rf "$PUBLISH_DIR"
        print_success "Removed publish directory: $PUBLISH_DIR"
    fi
fi

# Restore dependencies
print_status "Restoring NuGet packages..."
execute_command "dotnet restore $SOLUTION_PATH" "Restore dependencies"

# Build solution
print_status "Building solution..."
BUILD_ARGS="$SOLUTION_PATH --configuration $CONFIGURATION --no-restore"
if [ "$VERBOSE" = true ]; then
    BUILD_ARGS="$BUILD_ARGS --verbosity detailed"
fi
execute_command "dotnet build $BUILD_ARGS" "Build solution"

# Run tests if requested
if [ "$TEST" = true ]; then
    print_warning "Running tests..."
    
    TEST_ARGS="$TEST_PROJECT --configuration $CONFIGURATION --no-build --verbosity normal"
    
    if [ "$COVERAGE" = true ]; then
        TEST_ARGS="$TEST_ARGS --collect:\"XPlat Code Coverage\" --results-directory ./coverage"
        print_status "Code coverage enabled"
    fi
    
    execute_command "dotnet test $TEST_ARGS" "Run tests"
    
    if [ "$COVERAGE" = true ]; then
        print_status "Generating coverage report..."
        
        # Install reportgenerator if not present
        if ! command -v reportgenerator &> /dev/null; then
            print_status "Installing ReportGenerator..."
            execute_command "dotnet tool install -g dotnet-reportgenerator-globaltool" "Install ReportGenerator"
        fi
        
        # Generate HTML coverage report
        COVERAGE_FILE=$(find ./coverage -name "coverage.cobertura.xml" | head -n 1)
        if [ -n "$COVERAGE_FILE" ]; then
            execute_command "reportgenerator -reports:\"$COVERAGE_FILE\" -targetdir:\"./coverage/report\" -reporttypes:Html" "Generate coverage report"
            print_success "Coverage report generated at: ./coverage/report/index.html"
        fi
    fi
fi

# Publish if requested
if [ "$PUBLISH" = true ]; then
    print_warning "Publishing application..."
    
    if [ -z "$RUNTIME" ]; then
        print_error "Runtime must be specified for publishing. Use -r or --runtime parameter."
        exit 1
    fi
    
    PUBLISH_ARGS="$MAIN_PROJECT --configuration $CONFIGURATION --runtime $RUNTIME --self-contained true --output $PUBLISH_DIR/$RUNTIME"
    
    # Add platform-specific publish options
    case $RUNTIME in
        linux-x64|osx-x64)
            PUBLISH_ARGS="$PUBLISH_ARGS -p:PublishSingleFile=true -p:PublishTrimmed=true"
            ;;
        browser-wasm)
            # WebAssembly specific settings
            print_status "Installing WASM workload if needed..."
            execute_command "dotnet workload install wasm-tools" "Install WASM workload"
            ;;
    esac
    
    execute_command "dotnet publish $PUBLISH_ARGS" "Publish application"
    print_success "Application published to: $PUBLISH_DIR/$RUNTIME"
fi

# Create packages if requested
if [ "$PACKAGE" = true ]; then
    print_warning "Creating packages..."
    
    if [ "$PUBLISH" != true ]; then
        print_warning "Package creation requires publish. Running publish first..."
        # Determine default runtime based on OS
        if [[ "$OSTYPE" == "linux-gnu"* ]]; then
            DEFAULT_RUNTIME="linux-x64"
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            DEFAULT_RUNTIME="osx-x64"
        else
            DEFAULT_RUNTIME="linux-x64"
        fi
        
        # Recursive call with publish
        exec "$0" --configuration "$CONFIGURATION" --runtime "$DEFAULT_RUNTIME" --publish --package
    fi
    
    # Create packages directory
    PACKAGE_DIR="packages"
    mkdir -p "$PACKAGE_DIR"
    
    # Create packages for available runtimes
    for runtime_dir in "$PUBLISH_DIR"/*; do
        if [ -d "$runtime_dir" ]; then
            runtime_name=$(basename "$runtime_dir")
            package_name="pigeon-uno-$runtime_name.tar.gz"
            package_path="$PACKAGE_DIR/$package_name"
            
            print_status "Creating package: $package_name"
            tar -czf "$package_path" -C "$runtime_dir" .
            print_success "Package created: $package_path"
            
            # Create additional Linux packages
            if [ "$runtime_name" = "linux-x64" ]; then
                print_status "Creating DEB package..."
                
                # Create DEB package structure
                DEB_DIR="$PACKAGE_DIR/deb"
                mkdir -p "$DEB_DIR/DEBIAN"
                mkdir -p "$DEB_DIR/usr/bin"
                mkdir -p "$DEB_DIR/usr/share/applications"
                mkdir -p "$DEB_DIR/usr/share/pixmaps"
                
                # Copy application files
                cp -r "$runtime_dir"/* "$DEB_DIR/usr/bin/"
                
                # Create control file
                cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: pigeon-uno
Version: 1.0.0
Section: utils
Priority: optional
Architecture: amd64
Depends: libgtk-3-0, libwebkit2gtk-4.0-37
Maintainer: Pigeon GCS Team <dev@pigeon-gcs.com>
Description: Pigeon Ground Control Station
 Cross-platform ground control station for UAV/drone operations.
EOF
                
                # Create desktop entry
                cat > "$DEB_DIR/usr/share/applications/pigeon-uno.desktop" << EOF
[Desktop Entry]
Name=Pigeon Uno
Comment=Ground Control Station
Exec=/usr/bin/HarvestmoonGCS
Icon=pigeon-uno
Terminal=false
Type=Application
Categories=Utility;
EOF
                
                # Build DEB package
                if command -v dpkg-deb &> /dev/null; then
                    execute_command "dpkg-deb --build $DEB_DIR $PACKAGE_DIR/pigeon-uno_1.0.0_amd64.deb" "Build DEB package"
                    print_success "DEB package created"
                else
                    print_warning "dpkg-deb not found. Skipping DEB package creation."
                fi
                
                # Clean up
                rm -rf "$DEB_DIR"
            fi
        fi
    done
fi

# Summary
echo ""
print_success "Build completed successfully!"

if [ "$TEST" = true ]; then
    print_success "All tests passed"
fi

if [ "$PUBLISH" = true ]; then
    print_success "Application published for runtime: $RUNTIME"
fi

if [ "$PACKAGE" = true ]; then
    print_success "Distribution packages created"
fi

echo ""
echo -e "${BLUE}📊 Build Summary:${NC}"
echo "  Configuration: $CONFIGURATION"
if [ -n "$RUNTIME" ]; then
    echo "  Runtime: $RUNTIME"
fi
echo "  Tests: $([ "$TEST" = true ] && echo "Executed" || echo "Skipped")"
echo "  Coverage: $([ "$COVERAGE" = true ] && echo "Generated" || echo "Disabled")"
echo "  Publish: $([ "$PUBLISH" = true ] && echo "Completed" || echo "Skipped")"
echo "  Package: $([ "$PACKAGE" = true ] && echo "Created" || echo "Skipped")"

echo ""
echo -e "${GREEN}🎉 Build script completed successfully!${NC}"