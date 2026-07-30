#!/bin/zsh
set -euo pipefail

script_directory="${0:A:h}"
repository_root="${script_directory:h:h}"
package_path="$repository_root/apple/AttentionGuardianDomain"
output_root="$repository_root/work/macos-validation"
app_path="$output_root/Attention Guardian Validation.app"
contents_path="$app_path/Contents"
executable_name="AttentionGuardianMacApp"
bundle_identifier="local.attentionguardian.validation"

mkdir -p "$output_root"
rm -rf "$app_path"
mkdir -p "$contents_path/MacOS"

binary_path="$(swift build \
    --package-path "$package_path" \
    --product "$executable_name" \
    --show-bin-path)"
cp "$binary_path/$executable_name" \
    "$contents_path/MacOS/$executable_name"
chmod +x "$contents_path/MacOS/$executable_name"

plist_path="$contents_path/Info.plist"
plutil -create xml1 "$plist_path"
plutil -insert CFBundleDevelopmentRegion -string zh_CN "$plist_path"
plutil -insert CFBundleDisplayName \
    -string "Attention Guardian 验收版" "$plist_path"
plutil -insert CFBundleExecutable -string "$executable_name" "$plist_path"
plutil -insert CFBundleIdentifier -string "$bundle_identifier" "$plist_path"
plutil -insert CFBundleInfoDictionaryVersion -string 6.0 "$plist_path"
plutil -insert CFBundleName -string "Attention Guardian" "$plist_path"
plutil -insert CFBundlePackageType -string APPL "$plist_path"
plutil -insert CFBundleShortVersionString -string 0.1.0 "$plist_path"
plutil -insert CFBundleVersion -string 1 "$plist_path"
plutil -insert LSMinimumSystemVersion -string 14.0 "$plist_path"
plutil -insert NSHighResolutionCapable -bool true "$plist_path"

xattr -cr "$app_path"
codesign --force --sign - "$app_path"

if [[ "${1:-}" != "--build-only" ]]; then
    open "$app_path"
fi

print -r -- "$app_path"
