let
sources=import ./nix/sources.nix;
unstable= import sources.unstable{config.allowUnfree = true;};

pkgs=import sources.nixpkgs{};
in
#https://github.com/NixOS/nixpkgs/blob/183aeb2df252e6ace695b88b9c543d8178eeb0f9/doc/languages-frameworks/dotnet.section.md#packaging-a-dotnet-application
pkgs.mkShell {
  buildInputs = [
    unstable.dotnet-sdk_5
    unstable.ffmpeg-full
    unstable.steam-run
    # keep this line if you use bash
    pkgs.bashInteractive
  ];
  shellHook="
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
  set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
  ";
}
