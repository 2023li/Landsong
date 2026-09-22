using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Projectile catalog. It cannot resolve another domain.</summary>
    public sealed class ProjectileCatalogIndex
    {
        readonly Dictionary<ProjectileDefinitionAsset, ProjectileId> assets = new Dictionary<ProjectileDefinitionAsset, ProjectileId>();
        public ProjectileCatalogIndex(ProjectileCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Projectile 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Projectile 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, ProjectileId.FromIndex(i));
            }
        }

        public ProjectileId Resolve(ProjectileDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Projectile 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Projectile 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class ProjectileCatalogCompiler
    {
        public static BlobAssetReference<ProjectileCatalogBlob> Build(ProjectileCatalogAsset catalog)
        {
            var projectileIndex = new ProjectileCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<ProjectileCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i]);
                return builder.CreateBlobAssetReference<ProjectileCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, ProjectileDefinitionAsset source, ref ProjectileDefinition target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Projectile 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
        }
    }
}
