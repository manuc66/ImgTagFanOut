using System.Buffers.Binary;
using ImgTagFanOut.Models.Faces;

namespace ImgTagFanOut.Tests;

public class FaceClustererTests
{
    private readonly FaceClusterer _clusterer = new();

    private static FaceEmbedding Face(int id, float[] values)
    {
        double norm = Math.Sqrt(values.Sum(value => (double)value * value));
        byte[] bytes = new byte[values.Length * sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), (float)(values[i] / norm));
        }

        return new FaceEmbedding(id, $"photo-{id}.jpg", 0.1f, 0.1f, 0.2f, 0.2f, bytes);
    }

    [Fact]
    public void FacesOfTheSamePersonEndUpInOneCluster()
    {
        // Trois variantes proches, puis trois autres : deux groupes distincts.
        FaceEmbedding[] faces =
        [
            Face(1, [1.00f, 0.10f, 0.00f]),
            Face(2, [0.98f, 0.18f, 0.02f]),
            Face(3, [0.99f, 0.05f, 0.05f]),
            Face(4, [0.00f, 1.00f, 0.10f]),
            Face(5, [0.05f, 0.99f, 0.05f]),
            Face(6, [0.10f, 0.95f, 0.00f]),
        ];

        IReadOnlyList<FaceCluster> clusters = _clusterer.Cluster(faces);

        Assert.Equal(2, clusters.Count);
        Assert.Equal(3, clusters[0].Count);
        Assert.Equal(3, clusters[1].Count);
        Assert.Contains(clusters[0].Faces, face => face.FaceId == 1);
        Assert.Contains(clusters[1].Faces, face => face.FaceId == 4);
    }

    [Fact]
    public void AnIsolatedFaceIsNotWorthACluster()
    {
        FaceEmbedding[] faces =
        [
            Face(1, [1.00f, 0.05f, 0.00f]),
            Face(2, [0.99f, 0.10f, 0.02f]),
            Face(3, [0.00f, 0.00f, 1.00f]),
        ];

        IReadOnlyList<FaceCluster> clusters = _clusterer.Cluster(faces);

        Assert.Single(clusters);
        Assert.DoesNotContain(clusters[0].Faces, face => face.FaceId == 3);
    }

    [Fact]
    public void TheThresholdDecidesWhetherTwoPeopleAreMerged()
    {
        FaceEmbedding[] faces =
        [
            Face(1, [1.00f, 0.00f, 0.00f]),
            Face(2, [0.60f, 0.80f, 0.00f]),
        ];

        // cos = 0,6 : groupés à 0,55, séparés à 0,7 (et alors aucun groupe de 2).
        Assert.Single(_clusterer.Cluster(faces, threshold: 0.55f));
        Assert.Empty(_clusterer.Cluster(faces, threshold: 0.70f));
    }

    [Fact]
    public void ClustersAreOrderedBySize()
    {
        FaceEmbedding[] faces =
        [
            Face(1, [1.00f, 0.00f, 0.00f]),
            Face(2, [0.99f, 0.05f, 0.00f]),
            Face(3, [0.00f, 1.00f, 0.00f]),
            Face(4, [0.02f, 0.99f, 0.00f]),
            Face(5, [0.00f, 0.98f, 0.05f]),
        ];

        IReadOnlyList<FaceCluster> clusters = _clusterer.Cluster(faces);

        Assert.Equal(2, clusters.Count);
        Assert.Equal(3, clusters[0].Count);
        Assert.Equal(2, clusters[1].Count);
    }
}
