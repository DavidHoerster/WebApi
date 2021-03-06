﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Linq;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.OData.Extensions;
using System.Xml.Linq;
using Microsoft.OData.Core;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.OData.Formatter.Serialization
{
    public class ODataPrimitiveSerializerTests
    {
        public static IEnumerable<object[]> NonEdmPrimitiveConversionData
        {
            get
            {
                return EdmPrimitiveHelpersTest
                    .ConvertPrimitiveValue_NonStandardPrimitives_Data
                    .Select(data => new[] { data[1], data[0] });
            }
        }

        public static TheoryDataSet<DateTime, DateTimeOffset> NonEdmPrimitiveConversionDateTime
        {
            get
            {
                DateTime dtUtc = new DateTime(2014, 12, 12, 1, 2, 3, DateTimeKind.Utc);
                DateTime dtLocal = new DateTime(2014, 12, 12, 1, 2, 3, DateTimeKind.Local);
                DateTime unspecified = new DateTime(2014, 12, 12, 1, 2, 3, DateTimeKind.Unspecified);
                return new TheoryDataSet<DateTime, DateTimeOffset>
                {
                    { dtUtc, DateTimeOffset.Parse("2014-12-11T17:02:03-8:00") },
                    { dtLocal, new DateTimeOffset(dtLocal.ToUniversalTime()).ToOffset(new TimeSpan(-8, 0, 0)) },
                    { unspecified, DateTimeOffset.Parse("2014-12-12T01:02:03-8:00") }
                };
            }
        }

        public static TheoryDataSet<object> NonEdmPrimitiveData
        {
            get
            {
                return new TheoryDataSet<object>
                {
                    null,
                    (char)'1',
                    (char[]) new char[] {'1' },
                    (UInt16)1,
                    (UInt32)1,
                    (UInt64)1,
                    //(Stream) new MemoryStream(new byte[] { 1 }), // TODO: Enable once we have support for streams
                    new XElement(XName.Get("element","namespace")), 
                    new Binary(new byte[] {1}),
                    new DateTime(2014, 11, 19)
                };
            }
        }

        public static TheoryDataSet<object> EdmPrimitiveData
        {
            get
            {
                return new TheoryDataSet<object>
                {
                    null,
                    (string)"1",
                    (Boolean)true,
                    (Byte)1,
                    (Decimal)1,
                    (Double)1,
                    (Guid)Guid.Empty,
                    (Int16)1,
                    (Int32)1,
                    (Int64)1,
                    (SByte)1,
                    (Single)1,
                    new byte[] { 1 },
                    new TimeSpan(),
                    new DateTimeOffset()
                };
            }
        }

        [Fact]
        public void Property_ODataPayloadKind()
        {
            var serializer = new ODataPrimitiveSerializer();
            Assert.Equal(serializer.ODataPayloadKind, ODataPayloadKind.Property);
        }

        [Fact]
        public void WriteObject_Throws_RootElementNameMissing()
        {
            ODataSerializerContext writeContext = new ODataSerializerContext();
            ODataPrimitiveSerializer serializer = new ODataPrimitiveSerializer();

            Assert.Throws<ArgumentException>(
                () => serializer.WriteObject(42, typeof(int), ODataTestUtil.GetMockODataMessageWriter(), writeContext),
                "The 'RootElementName' property is required on 'ODataSerializerContext'.\r\nParameter name: writeContext");
        }

        [Fact]
        public void WriteObject_Calls_CreateODataPrimitiveValue()
        {
            ODataSerializerContext writeContext = new ODataSerializerContext { RootElementName = "Property", Model = EdmCoreModel.Instance };
            Mock<ODataPrimitiveSerializer> serializer = new Mock<ODataPrimitiveSerializer>();
            serializer.CallBase = true;
            serializer.Setup(s => s.CreateODataPrimitiveValue(
                    42, It.Is<IEdmPrimitiveTypeReference>(t => t.PrimitiveKind() == EdmPrimitiveTypeKind.Int32), writeContext))
                .Returns(new ODataPrimitiveValue(42)).Verifiable();

            serializer.Object.WriteObject(42, typeof(int), ODataTestUtil.GetMockODataMessageWriter(), writeContext);

            serializer.Verify();
        }

        [Fact]
        public void CreateODataValue_PrimitiveValue()
        {
            IEdmPrimitiveTypeReference edmPrimitiveType = EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(int));
            var serializer = new ODataPrimitiveSerializer();

            var odataValue = serializer.CreateODataValue(20, edmPrimitiveType, writeContext: null);
            Assert.NotNull(odataValue);
            ODataPrimitiveValue primitiveValue = Assert.IsType<ODataPrimitiveValue>(odataValue);
            Assert.Equal(primitiveValue.Value, 20);
        }

        [Fact]
        public void CreateODataValue_ReturnsODataNullValue_ForNullValue()
        {
            IEdmPrimitiveTypeReference edmPrimitiveType = EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(string));
            var serializer = new ODataPrimitiveSerializer();
            var odataValue = serializer.CreateODataValue(null, edmPrimitiveType, new ODataSerializerContext());

            Assert.IsType<ODataNullValue>(odataValue);
        }

        [Fact]
        public void CreateODataValue_ReturnsDateTimeOffset_ForDateTime_ByDefault()
        {
            // Arrange
            IEdmPrimitiveTypeReference edmPrimitiveType =
                EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(DateTime));
            ODataPrimitiveSerializer serializer = new ODataPrimitiveSerializer();
            DateTime dt = new DateTime(2014, 10, 27);

            // Act
            ODataValue odataValue = serializer.CreateODataValue(dt, edmPrimitiveType, new ODataSerializerContext());

            // Assert
            ODataPrimitiveValue primitiveValue = Assert.IsType<ODataPrimitiveValue>(odataValue);
            Assert.Equal(new DateTimeOffset(dt), primitiveValue.Value);
        }

        [Theory]
        [PropertyData("NonEdmPrimitiveConversionDateTime")]
        public void CreateODataValue_ReturnsDateTimeOffset_ForDateTime_WithDifferentTimeZone(DateTime value, DateTimeOffset expect)
        {
            // Arrange
            IEdmPrimitiveTypeReference edmPrimitiveType =
                EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(DateTime));
            ODataPrimitiveSerializer serializer = new ODataPrimitiveSerializer();

            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            HttpConfiguration configuration = new HttpConfiguration();
            configuration.SetTimeZoneInfo(tzi);

            HttpRequestMessage request = new HttpRequestMessage();
            request.SetConfiguration(configuration);

            ODataSerializerContext context = new ODataSerializerContext{ Request = request };

            // Act
            ODataValue odataValue = serializer.CreateODataValue(value, edmPrimitiveType, context);

            // Assert
            ODataPrimitiveValue primitiveValue = Assert.IsType<ODataPrimitiveValue>(odataValue);
            Assert.Equal(expect, primitiveValue.Value);
        }

        [Theory]
        [PropertyData("EdmPrimitiveData")]
        [PropertyData("NonEdmPrimitiveData")]
        public void WriteObject_EdmPrimitives(object graph)
        {
            IEdmPrimitiveTypeReference edmPrimitiveType = EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(int));
            var serializer = new ODataPrimitiveSerializer();
            ODataSerializerContext writecontext = new ODataSerializerContext() { RootElementName = "PropertyName", Model = EdmCoreModel.Instance };

            ODataMessageWriterSettings settings = new ODataMessageWriterSettings
            {
                ODataUri = new ODataUri { ServiceRoot = new Uri("http://any/"), }
            };
            ODataMessageWriter writer = new ODataMessageWriter(
                new ODataMessageWrapper(new MemoryStream()) as IODataResponseMessage,
                settings);

            Assert.DoesNotThrow(() => serializer.WriteObject(graph, typeof(int), writer, writecontext));
        }

        [Fact]
        public void AddTypeNameAnnotationAsNeeded_AddsAnnotation_InJsonLightMetadataMode()
        {
            // Arrange
            IEdmPrimitiveTypeReference edmPrimitiveType = EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(short));
            ODataPrimitiveValue primitive = new ODataPrimitiveValue((short)1);

            // Act
            ODataPrimitiveSerializer.AddTypeNameAnnotationAsNeeded(primitive, edmPrimitiveType, ODataMetadataLevel.FullMetadata);

            // Assert
            SerializationTypeNameAnnotation annotation = primitive.GetAnnotation<SerializationTypeNameAnnotation>();
            Assert.NotNull(annotation); // Guard
            Assert.Equal("Edm.Int16", annotation.TypeName);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(0, true)]
        [InlineData("", true)]
        [InlineData(0.1D, true)]
        [InlineData(double.PositiveInfinity, false)]
        [InlineData(double.NegativeInfinity, false)]
        [InlineData(double.NaN, false)]
        [InlineData((short)1, false)]
        public void CanTypeBeInferredInJson(object value, bool expectedResult)
        {
            // Act
            bool actualResult = ODataPrimitiveSerializer.CanTypeBeInferredInJson(value);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public void CreatePrimitive_ReturnsNull_ForNullValue()
        {
            // Act
            IEdmPrimitiveTypeReference edmPrimitiveType = EdmLibHelpers.GetEdmPrimitiveTypeReferenceOrNull(typeof(int));
            ODataValue value = ODataPrimitiveSerializer.CreatePrimitive(null, edmPrimitiveType, writeContext: null);

            // Assert
            Assert.Null(value);
        }

        [Theory]
        [PropertyData("EdmPrimitiveData")]
        public void ConvertUnsupportedPrimitives_DoesntChangeStandardEdmPrimitives(object graph)
        {
            Assert.Equal(
                graph,
                ODataPrimitiveSerializer.ConvertUnsupportedPrimitives(graph));
        }

        [Theory]
        [PropertyData("NonEdmPrimitiveConversionData")]
        public void ConvertUnsupportedPrimitives_NonStandardEdmPrimitives(object graph, object result)
        {
            Assert.Equal(
                result,
                ODataPrimitiveSerializer.ConvertUnsupportedPrimitives(graph));
        }

        [Theory]
        [PropertyData("NonEdmPrimitiveConversionDateTime")]
        public void ConvertUnsupportedDateTime_NonStandardEdmPrimitives(DateTime graph, DateTimeOffset result)
        {
            // Arrange & Act
            object value = ODataPrimitiveSerializer.ConvertUnsupportedDateTime(graph, timeZoneInfo: null);

            // Assert
            DateTimeOffset actual = Assert.IsType<DateTimeOffset>(value);
            Assert.Equal(new DateTimeOffset(graph), actual);
        }

        [Theory]
        [PropertyData("NonEdmPrimitiveConversionDateTime")]
        public void ConvertUnsupportedDateTime_NonStandardEdmPrimitives_TimeZone(DateTime graph, DateTimeOffset result)
        {
            // Arrange
            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            // Act
            object value = ODataPrimitiveSerializer.ConvertUnsupportedDateTime(graph, tzi);

            // Assert
            DateTimeOffset actual = Assert.IsType<DateTimeOffset>(value);
            Assert.Equal(result, actual);
        }

        [Theory]
        [InlineData(0, TestODataMetadataLevel.FullMetadata, true)]
        [InlineData((short)1, TestODataMetadataLevel.FullMetadata, false)]
        [InlineData((short)1, TestODataMetadataLevel.MinimalMetadata, true)]
        [InlineData((short)1, TestODataMetadataLevel.NoMetadata, true)]
        public void ShouldSuppressTypeNameSerialization(object value, TestODataMetadataLevel metadataLevel,
            bool expectedResult)
        {
            // Act
            bool actualResult = ODataPrimitiveSerializer.ShouldSuppressTypeNameSerialization(value,
                (ODataMetadataLevel)metadataLevel);

            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        public static TheoryDataSet<EdmPrimitiveTypeKind> EdmPrimitiveKinds
        {
            get
            {
                TheoryDataSet<EdmPrimitiveTypeKind> dataset = new TheoryDataSet<EdmPrimitiveTypeKind>();
                var primitiveKinds = Enum.GetValues(typeof(EdmPrimitiveTypeKind))
                                        .OfType<EdmPrimitiveTypeKind>()
                                        .Where(primitiveKind => primitiveKind != EdmPrimitiveTypeKind.None);

                foreach (var primitiveKind in primitiveKinds)
                {
                    dataset.Add(primitiveKind);
                }
                return dataset;
            }
        }
    }
}
