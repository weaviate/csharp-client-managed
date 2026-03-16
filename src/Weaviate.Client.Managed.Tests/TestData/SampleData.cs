namespace Weaviate.Client.Managed.Tests.TestData
{
    public static class SampleData
    {
        public static List<Article> Articles =>
            new()
            {
                new Article
                {
                    Id = Guid.NewGuid(),
                    Title = "Introduction to AI",
                    Content = "Artificial intelligence is transforming the world.",
                    WordCount = 150,
                    Price = 29.99,
                },
                new Article
                {
                    Id = Guid.NewGuid(),
                    Title = "Machine Learning Basics",
                    Content = "Machine learning is a subset of AI.",
                    WordCount = 200,
                    Price = 39.99,
                },
                new Article
                {
                    Id = Guid.NewGuid(),
                    Title = "Deep Learning Guide",
                    Content = "Deep learning uses neural networks.",
                    WordCount = 300,
                    Price = 49.99,
                },
            };

        public class Article
        {
            [WeaviateUUID]
            public Guid Id { get; set; }

            [Property(DataType.Text)]
            public string Title { get; set; } = string.Empty;

            [Property(DataType.Text)]
            public string Content { get; set; } = string.Empty;

            [Property(DataType.Int)]
            public int WordCount { get; set; }

            [Property(DataType.Number)]
            public double Price { get; set; }
        }
    }
}
