namespace Microsoft.Search.StructuredDataExtraction.Tests
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using OpenScraping;
    using OpenScraping.Config;
    using System;
    using System.Globalization;
    using System.IO;
    using Xunit;
    using FluentAssertions;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;

    public class StructuredDataExtractionTests
    {
        [Fact]
        public void QuoraWithWikiExtractionTest()
        {
            var configPath = Path.Combine("TestData", "quora.com.json");
            var config = StructuredDataConfig.ParseJsonFile(configPath);
            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(File.ReadAllText(Path.Combine("TestData", "quora.com.withwiki.html")));
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);

            var parsedJson = JObject.Parse(json);

            // Question
            var question = parsedJson["question"];

            json.Should().NotBeNull("Extractor should find a question in the HTML file");

            Assert.Equal("What can I learn/know right now in 10 minutes that will be useful for the rest of my life?", question["title"].Value<string>());

            // Answers
            Assert.NotNull(parsedJson["answers"]);
            Assert.Equal(5, parsedJson["answers"].Children().Count());

            // Best Answer
            Assert.NotNull(parsedJson["bestAnswer"]);

            var bestAnswer = parsedJson["bestAnswer"];
            Assert.NotNull(bestAnswer["content"]);
            Assert.True(bestAnswer["content"].Value<string>().Length > 0);
            Assert.Equal(9, bestAnswer["lists"].Children().Count());
            Assert.Equal(25, bestAnswer["lists"][1]["items"].Children().Count());

            // Check is textAboveLength exists in each list
            foreach (var answer in parsedJson["answers"])
            {
                var lists = answer["lists"];

                if (lists != null)
                {
                    foreach (var list in lists)
                    {
                        Assert.Equal(JTokenType.Integer, list["textAboveLength"].Type);
                        var textAboveLength = ((JValue)list["textAboveLength"]).ToObject<int>();
                        Assert.True(textAboveLength > 0, string.Format(CultureInfo.InvariantCulture, "textAboveLength was not greater than 0. The extracted value is: {0}", textAboveLength));
                    }
                }
            }

            var bestAnswerLists = bestAnswer["lists"];

            if (bestAnswerLists != null)
            {
                foreach (var list in bestAnswerLists)
                {
                    Assert.Equal(JTokenType.Integer, list["textAboveLength"].Type);
                    var textAboveLength = ((JValue)list["textAboveLength"]).ToObject<int>();
                    Assert.True(textAboveLength > 0, string.Format(CultureInfo.InvariantCulture, "textAboveLength was not greater than 0. The extracted value is: {0}", textAboveLength));
                }
            }
        }

        [Fact]
        public void RemoveXPathsExtractionTest()
        {
            var configPath = Path.Combine("TestData", "article_with_comments_div.json");
            var config = StructuredDataConfig.ParseJsonFile(configPath);
            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(File.ReadAllText(Path.Combine("TestData", "article_with_comments_div.html")));
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);

            var parsedJson = JObject.Parse(json);

            Assert.Equal("Article  title", parsedJson["title"].Value<string>());
            Assert.Equal("Para1 content Para2 content", parsedJson["body"].Value<string>());

        }

        [Fact]
        public void RegexTest()
        {
            var configPath = Path.Combine("TestData", "regex_rules.json");
            var config = StructuredDataConfig.ParseJsonFile(configPath);
            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(File.ReadAllText(Path.Combine("TestData", "article_with_date.html")));
            var actualJson = JsonConvert.SerializeObject(result, Formatting.Indented);
            var parsedActualJson = JObject.Parse(actualJson);

            var expectedJsonPath = Path.Combine("TestData", "regex_expected_result.json");
            var expectedJson = File.ReadAllText(expectedJsonPath);
            var parsedExpectedJson = JObject.Parse(expectedJson);

            Assert.True(JToken.DeepEquals(parsedActualJson, parsedExpectedJson));
        }

        [Fact]
        public void ParseDateTest()
        {
            var configPath = Path.Combine("TestData", "parse_date_rules.json");
            var config = StructuredDataConfig.ParseJsonFile(configPath);
            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(File.ReadAllText(Path.Combine("TestData", "article_with_date.html")));
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal(DateTime.Parse("2018-11-24T00:00:00"), parsedJson["parsedDateNoFormat"].Value);
            Assert.Equal(DateTime.Parse("2011-12-30T00:00:00"), parsedJson["parsedDateWithFormat"].Value);
            Assert.Equal(DateTime.Parse("2008-06-12T00:00:00"), parsedJson["parsedDateNoFormatWithProviderStyle"].Value);
        }

        [Fact]
        public void CastToIntegerTest()
        {
            var html = "<meta property=\"width\" content=\"1200\">";

            var configJson = @"
            {
                'width': {
                    '_xpath': '/meta[@property=\'width\']/@content',
                    '_transformation': 'CastToIntegerTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal(1200, parsedJson["width"].Value);
        }

        [Fact]
        public void HtmlDecodeTest()
        {
            var html = "<html><body><div id='content'>&lt;a href=''&gt;A link&lt;/a&gt;.</div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']',
                    '_transformation': 'HtmlDecodeTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("<a href=''>A link</a>.", parsedJson["text"].Value);
        }

        [Fact]
        public void HtmlEncodeTest()
        {
            var html = "<html><body><div id='content'>a < b</div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']',
                    '_transformation': 'HtmlEncodeTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("a &lt; b", parsedJson["text"].Value);
        }

        [Fact]
        public void UrlDecodeTest()
        {
            var html = "<html><body><div id='content'><a href='https://www.bing.com/search?q=hello+world'></a></div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']/a/@href',
                    '_transformation': 'UrlDecodeTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("https://www.bing.com/search?q=hello world", parsedJson["text"].Value);
        }

        [Fact]
        public void UrlEncodeTest()
        {
            var html = "<html><body><div id='content'><a href='hello world'></a></div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']/a/@href',
                    '_transformation': 'UrlEncodeTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("hello+world", parsedJson["text"].Value);
        }

        [Fact]
        public void ExtractTextTest()
        {
            var html = "<html><body><div id='content'><a href=''>A link</a>with adjacent text.</div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']',
                    '_transformation': 'ExtractTextTransformation'
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("A link with adjacent text.", parsedJson["text"].Value);
        }

        [Fact]
        public void RemoveExtraWhitespaceTransformationTest()
        {
            var html = "<html><body><div id='content'><a href=''>A link</a>with     adjacent text. &quot;the final frontier&quot;</div></body></html>";

            var configJson = @"
            {
                'text': {
                    '_xpath': '//div[@id=\'content\']',
                    '_transformations': [
                        'ExtractTextTransformation',
                        'HtmlDecodeTransformation',
                        'RemoveExtraWhitespaceTransformation'
                    ]
                }
            }
            ";

            var config = StructuredDataConfig.ParseJsonString(configJson);

            var extractor = new StructuredDataExtractor(config);
            var result = extractor.Extract(html);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            Assert.Equal("A link with adjacent text. \"the final frontier\"", parsedJson["text"].Value);
        }
    }
}
