using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Domain.Handlers {
    public class DateSerializerTests {
        [Fact]
        public void DateSerializer_ShouldSerializeAndDeserializeCorrectly() {
            // Arrange
            var testDate = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
            var options = new JsonSerializerOptions {
                Converters = { new DateSerializer() }
            };

            // Act
            var jsonString = JsonSerializer.Serialize((DateTime?)testDate, options);

            // Assert
            Assert.Equal("\"14/11/2026\"", jsonString);

            // Act
            var deserializedDate = JsonSerializer.Deserialize<DateTime?>(jsonString, options);

            // Assert
            Assert.NotNull(deserializedDate);
            Assert.Equal(testDate.Date, deserializedDate.Value.Date);
        }

        [Fact]
        public void DateSerializer_ShouldReturnNull_WhenDateStringIsInvalid() {
            // Arrange
            var invalidJson = "\"99/99/9999\"";
            var options = new JsonSerializerOptions {
                Converters = { new DateSerializer() }
            };

            // Act
            var result = JsonSerializer.Deserialize<DateTime?>(invalidJson, options);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DateSerializer_ShouldWriteNull_WhenValueIsNull() {
            // Arrange
            var converter = new DateSerializer();
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            // Act
            converter.Write(writer, null, new JsonSerializerOptions());
            writer.Flush();
            var jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());

            // Assert
            Assert.Equal("null", jsonString);
        }
    }
}
