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
validation_executable_path="$contents_path/MacOS/$executable_name"

wait_for_validation_exit() {
    local running_pid="$1"
    local attempt
    for attempt in {1..30}; do
        if ! kill -0 "$running_pid" 2>/dev/null; then
            return 0
        fi
        sleep 0.1
    done
    print -u2 -r -- \
        "Validation app process $running_pid did not exit in time."
    return 1
}

if [[ "${1:-}" != "--build-only" ]]; then
    running_pids=(${(f)"$(pgrep -f -x \
        "$validation_executable_path" || true)"})
    for running_pid in "${running_pids[@]}"; do
        kill "$running_pid"
        wait_for_validation_exit "$running_pid"
    done
fi

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
