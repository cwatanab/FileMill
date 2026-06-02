# FileMill Makefile
# .NET 10 WPF アプリケーション

PROJECT := FileMill.csproj
CONFIG  := Release
OUTDIR  := bin/$(CONFIG)/net10.0-windows
EXE     := $(OUTDIR)/FileMill.exe

.PHONY: all build clean rebuild run publish

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
