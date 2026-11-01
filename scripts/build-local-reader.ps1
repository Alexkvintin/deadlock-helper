$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bundledCargo=Join-Path $root 'tools/toolchain/cargo/bin/cargo.exe'
$env:RUSTFLAGS='-C target-feature=+crt-static'
if(Test-Path $bundledCargo){
    $env:CARGO_HOME=Join-Path $root 'tools/toolchain/cargo'
    $env:RUSTUP_HOME=Join-Path $root 'tools/toolchain/rustup'
    $mingw=Join-Path $root 'tools/toolchain/llvm-mingw-20261020-ucrt-x86_64/bin'
    $env:PATH=$mingw+';'+$env:PATH
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_GNULLVM_LINKER='x86_64-w64-mingw32-clang.exe'
    & $bundledCargo +stable-x86_64-pc-windows-gnullvm build --locked --release --manifest-path "$root/native-reader/Cargo.toml"
    $readerBinary=Join-Path $root 'native-reader/target/release/companion-reader.exe'
}else{
    $cargo=(Get-Command cargo -ErrorAction Stop).Source
    & $cargo +stable build --locked --release --target x86_64-pc-windows-msvc --manifest-path "$root/native-reader/Cargo.toml"
    $readerBinary=Join-Path $root 'native-reader/target/x86_64-pc-windows-msvc/release/companion-reader.exe'
}
if($LASTEXITCODE -ne 0){throw 'Local reader build failed'}
New-Item -ItemType Directory -Force "$root/dist" | Out-Null
Copy-Item $readerBinary "$root/dist/companion-reader.exe" -Force
New-Item -ItemType Directory -Force "$root/dist/local-reader-licenses" | Out-Null
Copy-Item "$root/native-reader/licenses/*" "$root/dist/local-reader-licenses" -Recurse -Force
