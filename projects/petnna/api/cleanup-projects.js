import { list, del } from '@vercel/blob';

// Exponential backoff helper for blob deletion
async function deleteBatchWithBackoff(urls, retries = 5, delay = 1000) {
  for (let attempt = 1; attempt <= retries; attempt++) {
    try {
      await del(urls);
      return;
    } catch (error) {
      if (attempt === retries) {
        throw error;
      }
      console.warn(`[Batch Delete] Attempt ${attempt} failed: ${error.message}. Retrying in ${delay}ms...`);
      await new Promise((resolve) => setTimeout(resolve, delay));
      delay *= 2; // Double the delay
    }
  }
}

export default async function handler(req, res) {
  // Verify authorization for cron job (optional but recommended for security)
  // Vercel Cron sends a CRON_SECRET header or an Authorization header
  const authHeader = req.headers.authorization;
  const cronSecret = process.env.CRON_SECRET;
  
  if (cronSecret && authHeader !== `Bearer ${cronSecret}`) {
    return res.status(401).json({ error: 'Unauthorized' });
  }

  const vercelToken = process.env.VERCEL_TOKEN;
  if (!vercelToken) {
    return res.status(500).json({ error: 'VERCEL_TOKEN is not configured.' });
  }

  const teamId = process.env.VERCEL_TEAM_ID;
  const teamQuery = teamId ? `?teamId=${teamId}` : '';
  const headers = {
    Authorization: `Bearer ${vercelToken}`,
    'Content-Type': 'application/json',
  };

  const report = {
    projectsDeleted: [],
    blobsDeletedCount: 0,
    errors: [],
  };

  // 1. Clean up Vercel projects starting with "temp-project-" older than 12 hours
  try {
    const listRes = await fetch(`https://api.vercel.com/v9/projects${teamQuery}`, {
      headers,
    });
    
    if (!listRes.ok) {
      const errText = await listRes.text();
      throw new Error(`Failed to list projects: ${listRes.status} ${errText}`);
    }

    const { projects } = await listRes.json();
    const twelveHoursAgo = Date.now() - 12 * 60 * 60 * 1000;

    const tempProjects = (projects || []).filter(
      (p) => p.name.startsWith('temp-project-') && p.createdAt < twelveHoursAgo
    );

    for (const project of tempProjects) {
      try {
        const deleteRes = await fetch(
          `https://api.vercel.com/v9/projects/${project.id}${teamQuery}`,
          {
            method: 'DELETE',
            headers,
          }
        );

        if (deleteRes.ok) {
          report.projectsDeleted.push(project.name);
          console.log(`Deleted project: ${project.name} (${project.id})`);
        } else {
          const errText = await deleteRes.text();
          report.errors.push(`Failed to delete project ${project.name}: ${errText}`);
        }
      } catch (err) {
        report.errors.push(`Error deleting project ${project.name}: ${err.message}`);
      }
    }
  } catch (err) {
    report.errors.push(`Projects cleanup failed: ${err.message}`);
  }

  // 2. Clean up Vercel Blobs older than 30 days
  const blobToken = process.env.BLOB_READ_WRITE_TOKEN;
  if (blobToken) {
    try {
      let cursor;
      const BATCH_SIZE = 100;
      const thirtyDaysAgo = Date.now() - 30 * 24 * 60 * 60 * 1000;
      const urlsToDelete = [];

      do {
        const listResult = await list({
          cursor,
          limit: BATCH_SIZE,
          token: blobToken,
        });

        const oldBlobs = (listResult.blobs || []).filter(
          (blob) => new Date(blob.uploadedAt).getTime() < thirtyDaysAgo
        );

        for (const blob of oldBlobs) {
          urlsToDelete.push(blob.url);
        }

        cursor = listResult.cursor;
      } while (cursor);

      // Delete in chunks of 100
      for (let i = 0; i < urlsToDelete.length; i += BATCH_SIZE) {
        const batch = urlsToDelete.slice(i, i + BATCH_SIZE);
        try {
          await deleteBatchWithBackoff(batch);
          report.blobsDeletedCount += batch.length;
          console.log(`Deleted ${batch.length} blobs in batch.`);
        } catch (err) {
          report.errors.push(`Failed to delete blob batch starting at index ${i}: ${err.message}`);
        }
      }
    } catch (err) {
      report.errors.push(`Blob cleanup failed: ${err.message}`);
    }
  } else {
    console.log('BLOB_READ_WRITE_TOKEN is not configured. Skipping Vercel Blob cleanup.');
  }

  return res.status(200).json(report);
}
