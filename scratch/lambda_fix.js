import { DynamoDBClient } from "@aws-sdk/client-dynamodb";
import { GetItemCommand } from "@aws-sdk/client-dynamodb";
import { DynamoDBDocumentClient, GetCommand, QueryCommand } from "@aws-sdk/lib-dynamodb";
import { S3Client, ListObjectsV2Command, GetObjectCommand } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";

// Initialize DynamoDB and S3 client (reuse across invocations)
const client = new DynamoDBClient({});
const dynamo = DynamoDBDocumentClient.from(client);
const s3Client = new S3Client({});

export const handler = async (event) => {
  const headers = event.headers || {};
  const deviceId = headers["x-device-id"] || headers["X-Device-Id"];

  if (!deviceId) {
    return response(400, { error: "Missing deviceId" });
  }

  try {
    const id = event.queryStringParameters?.id;
    if (!id || id.trim() === '') {
      return response(400, { error: "Missing or invalid id parameter" });
    }

    const [poiResult, descResult] = await Promise.all([
      dynamo.send(new GetCommand({
        TableName: process.env.POI_TABLE_NAME || "Point_Of_Interest",
        Key: { PoiId: id.trim() }
      })),
      dynamo.send(new QueryCommand({
        TableName: process.env.POI_DESC_TABLE_NAME || "Poi_Description",
        IndexName: "search_index",
        KeyConditionExpression: "PoiId = :id",
        ExpressionAttributeValues: { ":id": id }
      }))
    ]);

    // 🔴 FIX 1: RESPECT IS_DELETED FLAG 🔴
    if (!poiResult.Item || poiResult.Item.isDeleted === true || poiResult.Item.IsDeleted === true) {
      return response(404, { error: "POI not found or is deleted" });
    }

    const checkResult = await client.send(
      new GetItemCommand({
        TableName: "QR_Purchased_Devices",
        Key: { deviceid: { S: deviceId } }
      })
    );
    
    if (!checkResult.Item || !checkResult.Item.ispaid?.BOOL) {
      return response(403, { error: "Device not purchased" });
    }

    const purchaseDtStr = checkResult.Item.purchasetime?.S;
    if (purchaseDtStr) {
      const ONE_DAY_MS = 24 * 60 * 60 * 1000;
      let purchaseTime = new Date(purchaseDtStr);
      if (isNaN(purchaseTime.getTime())) {
        purchaseTime = new Date(parseInt(purchaseDtStr));
      }
      const timeElapsed = Date.now() - purchaseTime.getTime();
      if (timeElapsed > ONE_DAY_MS) {
         return response(403, { error: "Device subscription expired", isExpired: true });
      }
    }

    const bucket = "audio-storage-system";
    const region = "ap-southeast-1";  
    const languages = {};
    if (descResult.Items && descResult.Items.length > 0) {
      for (const item of descResult.Items) {
        if (item.Language) {
          languages[item.Language] = {
            name: item.Name || null,
            description: item.Description || null,
            detail: item.Detail || null,        
            address: item.Address || null,        
            audioUrl: item.audioUrl || `https://${bucket}.s3.${region}.amazonaws.com/audio/${id}/${item.Language}.mp3`
          };
        }
      }
    }

    const imageBucket = "poi-image-storage";
    const imageRegion = "ap-southeast-1";  
    let imageUrls = [];
    try {
      const s3Command = new ListObjectsV2Command({
        Bucket: imageBucket,
        Prefix: `POI_${id.trim()}/`
      });
      const s3Response = await s3Client.send(s3Command);
      
      if (s3Response.Contents) {
        const validFiles = s3Response.Contents.filter(obj => obj.Size > 0);
        imageUrls = await Promise.all(validFiles.map(async obj => {
          const getCmd = new GetObjectCommand({
            Bucket: imageBucket,
            Key: obj.Key
          });
          return await getSignedUrl(s3Client, getCmd, { expiresIn: 3600 });
        }));
      }
    } catch (s3Err) {
      console.error("Error listing S3 images:", s3Err);
    }
    
    const poi = poiResult.Item;
    return response(200, {
      id,
      lat: typeof poi.Latitude === 'number' ? poi.Latitude : null,
      lng: typeof poi.Longitude === 'number' ? poi.Longitude : null,
      rating: typeof poi.Rating === 'number' ? poi.Rating : null,
      images: imageUrls,
      languages,
    });

  } catch (err) {
    console.error('Error processing request:', err);
    return response(500, { error: "Internal server error" });
  }
};

function response(statusCode, body) {
  return {
    statusCode,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  };
}
