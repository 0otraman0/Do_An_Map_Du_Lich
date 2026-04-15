import { DynamoDBClient } from "@aws-sdk/client-dynamodb";
import { DynamoDBDocumentClient, ScanCommand } from "@aws-sdk/lib-dynamodb";
import { S3Client, ListObjectsV2Command } from "@aws-sdk/client-s3";

const ddbClient = new DynamoDBClient({});
const dynamo = DynamoDBDocumentClient.from(ddbClient);
const s3 = new S3Client({});

const BUCKET = "poi-image-storage";

export const handler = async (event) => {
  // CORS Preflight
  if (event.requestContext?.http?.method === "OPTIONS") {
    return response(200, "");
  }

  try {
    // 1. SCAN TẤT CẢ POI
    let allPois = [];
    let lastKey = undefined;
    do {
      const result = await dynamo.send(new ScanCommand({
        TableName: "Point_Of_Interest",
        ExclusiveStartKey: lastKey
      }));
      allPois = allPois.concat(result.Items || []);
      lastKey = result.LastEvaluatedKey;
    } while (lastKey);

    // 2. PHÂN LOẠI POI (Active vs Deleted)
    // MAUI app expects "deletedids" array of IDs to remove from SQLite
    const activePois = [];
    const deletedids = [];

    allPois.forEach(poi => {
      // Kiểm tra cả isDeleted (JSON key) và IsDeleted (PascalCase mapping)
      if (poi.isDeleted === true || poi.IsDeleted === true) {
        deletedids.push(poi.PoiId.toString());
      } else {
        activePois.push(poi);
      }
    });

    // 3. SCAN TẤT CẢ DESCRIPTIONS
    let descriptions = [];
    lastKey = undefined;
    do {
      const result = await dynamo.send(new ScanCommand({
        TableName: "Poi_Description",
        ExclusiveStartKey: lastKey
      }));
      descriptions = descriptions.concat(result.Items || []);
      lastKey = result.LastEvaluatedKey;
    } while (lastKey);

    // 4. GET ALL IMAGES TỪ S3 (Sync image folder structure)
    const s3Data = await s3.send(new ListObjectsV2Command({
      Bucket: BUCKET,
      Prefix: "POI_"
    }));

    const imageMap = {};
    if (s3Data.Contents) {
      s3Data.Contents.forEach(obj => {
        const key = obj.Key;
        if (key.endsWith("/") || obj.Size === 0) return;
        
        const segments = key.split("/");
        if (segments.length < 2) return;
        
        const poiId = segments[0].replace("POI_", "");
        if (!imageMap[poiId]) imageMap[poiId] = [];

        imageMap[poiId].push(`https://${BUCKET}.s3.amazonaws.com/${key}`);
      });
    }

    // 5. HARDCODE LANGUAGES (OR FETCH FROM DB IF YOU HAVE A TABLE)
    const languages = [
      { Language_Code: "en", Language_Name: "English", Full_Language_Code: "en-US" },
      { Language_Code: "vi", Language_Name: "Tiếng Việt", Full_Language_Code: "vi-VN" }
    ];

    // 6. BUILD RESPONSE MATCHING C# PoiApiResponse CLASS
    const finalResponse = {
      updated: true,
      newlastupdated: Date.now(),
      pois: activePois,
      deletedids: deletedids,
      descriptions: descriptions,
      images: imageMap,
      languages: languages
    };

    return response(200, finalResponse);

  } catch (err) {
    console.error("Sync Error:", err);
    return response(500, { error: err.message });
  }
};

// HELPER: Consistent Response with CORS
function response(statusCode, body) {
  return {
    statusCode,
    headers: {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers": "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token,X-Device-Id",
      "Access-Control-Allow-Methods": "OPTIONS,GET,POST",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body)
  };
}
