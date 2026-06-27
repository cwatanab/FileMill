# FileMill Makefile
# .NET 10 WPF アプリケーション

PROJECT := FileMill.csproj
CONFIG  := Release
OUTDIR  := bin/$(CONFIG)/net10.0-windows
EXE     := $(OUTDIR)/FileMill.exe
TAG     ?= 0.3.2
BRANCH  := $(shell git branch --show-current 2>/dev/null)
PACKAGE := FileMill-$(TAG).zip
DISTDIR := dist/FileMill-$(TAG)
GH      := $(shell if command -v gh >/dev/null 2>&1; then printf gh; elif command -v gh.exe >/dev/null 2>&1; then printf gh.exe; fi)

.PHONY: all build clean rebuild run publish package release release-check

all: build

clean:
	dotnet clean $(PROJECT) -c $(CONFIG)
	@rm -rf bin obj

rebuild: clean build

run:
	dotnet run --project $(PROJECT) -c $(CONFIG)

publish:
	dotnet publish $(PROJECT) -c $(CONFIG) -o publish/

# ビルド後に exe のパスを表示
build:
	dotnet build $(PROJECT) -c $(CONFIG)
	@echo ""
	@echo "Build complete: $(EXE)"

release-check:
	@if [ -z "$(TAG)" ]; then \
		echo "[ERROR] TAG cannot be empty. Usage: make release TAG=0.3.2"; \
		exit 1; \
	fi
	@if [ -z "$(GH)" ]; then \
		echo "[ERROR] GitHub CLI gh is not installed."; \
		echo "Please install it from https://cli.github.com/"; \
		exit 1; \
	fi
	@if ! "$(GH)" auth status >/dev/null 2>&1; then \
		echo "[ERROR] GitHub CLI is not authenticated."; \
		echo "Please run 'gh auth login' to authenticate with GitHub."; \
		exit 1; \
	fi
	@if command -v powershell.exe >/dev/null 2>&1; then \
		powershell.exe -NoProfile -Command " \
			echo '==================================================='; \
			echo ' Checking External Tools Versions'; \
			echo '==================================================='; \
			\
			\$$oxipngLocal = (tools/oxipng.exe --version 2>\$$null) -replace 'oxipng\s+', ''; \
			if (-not \$$oxipngLocal) { \$$oxipngLocal = 'Not found' }; \
			\$$oxipngLatest = (gh api repos/shssoichiro/oxipng/releases/latest --jq .tag_name 2>\$$null) -replace '^v', ''; \
			if (-not \$$oxipngLatest) { \$$oxipngLatest = 'Unknown' }; \
			\
			\$$qpdfLocal = ((tools/qpdf.exe --version 2>\$$null)[0] -replace 'qpdf version\s+', '').Trim(); \
			if (-not \$$qpdfLocal) { \$$qpdfLocal = 'Not found' }; \
			\$$qpdfLatest = (gh api repos/qpdf/qpdf/releases/latest --jq .tag_name 2>\$$null) -replace '^v', ''; \
			if (-not \$$qpdfLatest) { \$$qpdfLatest = 'Unknown' }; \
			\
			\$$libjxlLatest = (gh api repos/libjxl/libjxl/releases/latest --jq .tag_name 2>\$$null) -replace '^v', ''; \
			if (-not \$$libjxlLatest) { \$$libjxlLatest = 'Unknown' }; \
			\
			\$$ffmpegLocal = (tools/ffmpeg.exe -version 2>\$$null)[0]; \
			if (\$$ffmpegLocal -match 'version\s+(N-\d+|[0-9\.]+)') { \$$ffmpegVer = \$$Matches[1] } else { \$$ffmpegVer = 'Not found' }; \
			\
			echo ('  - oxipng : Local=' + \$$oxipngLocal + ' / Latest=' + \$$oxipngLatest); \
			echo ('  - qpdf   : Local=' + \$$qpdfLocal + ' / Latest=' + \$$qpdfLatest); \
			echo ('  - cjpegli: (libjxl Latest=' + \$$libjxlLatest + ')'); \
			echo ('  - ffmpeg : Local=' + \$$ffmpegVer); \
			\
			if (\$$oxipngLocal -ne 'Not found' -and \$$oxipngLatest -ne 'Unknown' -and \$$oxipngLocal -ne \$$oxipngLatest) { \
				echo '  [WARNING] oxipng is outdated! Latest is ' + \$$oxipngLatest; \
			}; \
			if (\$$qpdfLocal -ne 'Not found' -and \$$qpdfLatest -ne 'Unknown' -and \$$qpdfLocal -ne \$$qpdfLatest) { \
				echo '  [WARNING] qpdf is outdated! Latest is ' + \$$qpdfLatest; \
			}; \
			echo '==================================================='; \
			echo ''; \
		"; \
	fi
	#@if ! git diff-index --quiet HEAD --; then \
	#	echo "[WARNING] You have uncommitted changes in your working tree."; \
	#	printf "Do you want to continue despite uncommitted changes? (y/N): "; \
	#	read confirm_dirty; \
	#	case "$$confirm_dirty" in [Yy]) ;; *) echo "Release aborted."; exit 1 ;; esac; \
	#	echo ""; \
	#fi

package: build
	@echo ""
	@echo "[2/5] Packaging release files..."
	@rm -rf dist "$(PACKAGE)"
	@mkdir -p "$(DISTDIR)"
	@cp -r "$(OUTDIR)"/* "$(DISTDIR)/"
	@rm -f "$(DISTDIR)"/*.pdb
	@if command -v powershell.exe >/dev/null 2>&1; then \
		powershell.exe -NoProfile -Command "Compress-Archive -Path '$(DISTDIR)' -DestinationPath '$(PACKAGE)' -Force"; \
	elif command -v powershell >/dev/null 2>&1; then \
		powershell -NoProfile -Command "Compress-Archive -Path '$(DISTDIR)' -DestinationPath '$(PACKAGE)' -Force"; \
	elif command -v zip >/dev/null 2>&1; then \
		(cd dist && zip -r "../$(PACKAGE)" "FileMill-$(TAG)"); \
	else \
		echo "[ERROR] Neither powershell nor zip command was found to compress the release folder."; \
		rm -rf dist; \
		exit 1; \
	fi
	@if [ ! -f "$(PACKAGE)" ]; then \
		echo "[ERROR] Failed to create ZIP archive."; \
		rm -rf dist; \
		exit 1; \
	fi
	@rm -rf dist
	@echo "Packaged successfully: $(PACKAGE)"

release: release-check
	@echo "==================================================="
	@echo " FileMill GitHub Release Automator"
	@echo "==================================================="
	@echo ""
	@echo "Release Configuration:"
	@echo "  - Tag Version  : $(TAG)"
	@echo "  - Git Branch   : $(BRANCH)"
	@echo "  - ZIP Package  : $(PACKAGE)"
	@echo ""
	#@printf "Are you sure you want to build and publish this release? (y/N): "; \
	#read confirm_release; \
	#case "$$confirm_release" in [Yy]) ;; *) echo "Release aborted."; exit 1 ;; esac
	@echo ""
	@echo "[1/5] Building release binaries..."
	@$(MAKE) package TAG="$(TAG)" CONFIG="$(CONFIG)"
	@echo ""
	@echo "[3/5] Creating Git tag '$(TAG)'..."
	@if ! git tag -a "$(TAG)" -m "$(TAG) をリリース" 2>/dev/null; then \
		echo "[WARNING] Git tag '$(TAG)' already exists locally or failed to create."; \
		printf "Proceed using the existing tag? (y/N): "; \
		read confirm_tag; \
		case "$$confirm_tag" in [Yy]) ;; *) echo "Release aborted."; exit 1 ;; esac; \
	else \
		echo "Git tag '$(TAG)' successfully created."; \
	fi
	@echo ""
	@echo "[4/5] Pushing branch '$(BRANCH)' and tags to GitHub..."
	@git push origin "$(BRANCH)" --tags
	@echo ""
	@echo "[5/5] Creating GitHub Release and uploading $(PACKAGE)..."
	@"$(GH)" release create "$(TAG)" "$(PACKAGE)" --title "FileMill $(TAG)" --notes "$(TAG) をリリースしました。"
	@echo ""
	@echo "==================================================="
	@echo " Release '$(TAG)' successfully published to GitHub!"
	@echo "==================================================="
