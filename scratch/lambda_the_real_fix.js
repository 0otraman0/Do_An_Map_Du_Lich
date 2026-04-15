import { DynamoDBClient } from "@aws-sdk/client-dynamodb";
import { DynamoDBDocumentClient, ScanCommand } from "@aws-sdk/lib-dynamodb";
import { S3Client, ListObjectsV2Command } from "@aws-sdk/client-s3";

const client = new DynamoDBClient({ region: "ap-southeast-1" });
const dynamo = DynamoDBDocumentClient.from(client);
const s3 = new S3Client({ region: "ap-southeast-1" });
const BUCKET = "poi-image-storage";

export const handler = async (event) => {
    // CORS PREFLIGHT
    if (event.requestContext?.http?.method === "OPTIONS") {
        return {
            statusCode: 200,
            headers: {
                "Access-Control-Allow-Origin": "*",
                "Access-Control-Allow-Headers": "*",
                "Access-Control-Allow-Methods": "OPTIONS,POST,GET"
            },
            body: ""
        };
    }

    const lastUpdated = Number(event.queryStringParameters?.lastUpdated || 0);
    const lang = event.queryStringParameters?.lang?.trim().toLowerCase();
    
    // 1. Get all POIs
    const poiResult = await dynamo.send(new ScanCommand({
        TableName: "Point_Of_Interest",
    }));

    let allItems = poiResult.Items || [];
    
    // 🔥 FIX 1: FILTER BY TIMESTAMP AND CASING 🔥
    // Your DB uses "IsDeleted" (PascalCase) and "PoiId"
    let updatedItems = allItems.filter(p => Number(p.lastUpdated || 0) >= lastUpdated);
    
    // Determine active vs deleted
    const activePois = updatedItems.filter(p => {
        const deleted = p.IsDeleted ?? p.isDeleted;
        return deleted !== true && deleted !== "true" && deleted !== 1;
    });
    
    const deletedPois = updatedItems.filter(p => {
        const deleted = p.IsDeleted ?? p.isDeleted;
        return deleted === true || deleted === "true" || deleted === 1;
    });
    
    const deletedids = deletedPois.map(p => String(p.PoiId));
    
    if (updatedItems.length === 0) {
        return response(200, { updated: false });
    }

    // 2. Get descriptions
    const descResult = await dynamo.send(new ScanCommand({
        TableName: "Poi_Description"
    }));
    
    const activeIds = new Set(activePois.map(p => String(p.PoiId)));
    
    let descriptions = (descResult.Items || []).filter(d =>
        activeIds.has(String(d.PoiId)) &&
        (!lang || d.Language === lang)
    );
    
    // 3. Get languages
    const langResult = await dynamo.send(new ScanCommand({
        TableName: "Language"
    }));
    const languages = langResult.Items || [];

    // 4. Get images from S3
    const images = {};
    for (const poi of activePois) {
        const poiId = poi.PoiId;
        const prefix = `POI_${poiId}/`;

        try {
            const imgResult = await s3.send(new ListObjectsV2Command({
                Bucket: BUCKET,
                Prefix: prefix
            }));
            images[poiId] = (imgResult.Contents || [])
                .filter(obj => obj.Size > 0)
                .map(obj => `https://${BUCKET}.s3.amazonaws.com/${obj.Key}`);
        } catch (e) {
            images[poiId] = [];
        }
    }
    
    // Get Last update
    const newlastupdated = Math.max(0, ...allItems.map(p => Number(p.lastUpdated || 0)));
    
    // 5. Build Final Response
    // 🔥 FIX 2: RETURN 'activePois' NOT THE FULL LIST 🔥
    console.log(`Syncing: ${activePois.length} Active, ${deletedids.length} Deleted`);
    
    return response(200, {
        updated: true,
        newlastupdated,
        languages,
        pois: activePois, // Only send active ones
        descriptions,
        deletedids,      // Send IDs to remove from SQLite
        images
    });
};

function response(statusCode, body) {
    return {
        statusCode,
        headers: {
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Headers": "*",
            "Access-Control-Allow-Methods": "OPTIONS,GET,POST",
            "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
    };
}
