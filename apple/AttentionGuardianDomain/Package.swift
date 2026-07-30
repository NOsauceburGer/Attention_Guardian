// swift-tools-version: 6.2

import PackageDescription

let package = Package(
    name: "AttentionGuardianDomain",
    platforms: [
        .macOS(.v14),
        .iOS(.v17)
    ],
    products: [
        .library(
            name: "AttentionGuardianDomain",
            targets: ["AttentionGuardianDomain"]),
        .library(
            name: "AttentionGuardianApplication",
            targets: ["AttentionGuardianApplication"]),
        .library(
            name: "AttentionGuardianPersistence",
            targets: ["AttentionGuardianPersistence"]),
        .library(
            name: "AttentionGuardianInfrastructure",
            targets: ["AttentionGuardianInfrastructure"]),
        .library(
            name: "AttentionGuardianPresentation",
            targets: ["AttentionGuardianPresentation"]),
        .executable(
            name: "AttentionGuardianMacApp",
            targets: ["AttentionGuardianMacApp"])
    ],
    targets: [
        .systemLibrary(
            name: "CSQLite",
            pkgConfig: "sqlite3",
            providers: [
                .brew(["sqlite3"]),
                .apt(["libsqlite3-dev"])
            ]),
        .target(name: "AttentionGuardianDomain"),
        .target(
            name: "AttentionGuardianApplication",
            dependencies: ["AttentionGuardianDomain"]),
        .target(
            name: "AttentionGuardianPersistence",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain",
                "CSQLite"
            ]),
        .target(
            name: "AttentionGuardianInfrastructure",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain"
            ]),
        .target(
            name: "AttentionGuardianPresentation",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain"
            ]),
        .executableTarget(
            name: "AttentionGuardianMacApp",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianInfrastructure",
                "AttentionGuardianPersistence",
                "AttentionGuardianPresentation"
            ]),
        .testTarget(
            name: "AttentionGuardianDomainTests",
            dependencies: ["AttentionGuardianDomain"]),
        .testTarget(
            name: "AttentionGuardianApplicationTests",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain"
            ]),
        .testTarget(
            name: "AttentionGuardianPersistenceTests",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain",
                "AttentionGuardianPersistence",
                "CSQLite"
            ]),
        .testTarget(
            name: "AttentionGuardianInfrastructureTests",
            dependencies: [
                "AttentionGuardianApplication",
                "AttentionGuardianDomain",
                "AttentionGuardianInfrastructure"
            ]),
        .testTarget(
            name: "AttentionGuardianPresentationTests",
            dependencies: ["AttentionGuardianPresentation"])
    ]
)
