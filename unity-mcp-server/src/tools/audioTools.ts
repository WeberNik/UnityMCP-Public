// ============================================================================
// UnityVision MCP Server - Audio Tools
// Tools for audio inspection and preview
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const audioToolDefinitions = [
  {
    name: 'list_audio_sources',
    description: `List all AudioSource components in the scene.

**Returns:**
- All AudioSources with their settings
- Clip names, volumes, spatial settings
- Playing state (in Play mode)

**Use Cases:**
- "What audio sources are in the scene?"
- "Which sounds are set to loop?"
- Audio debugging`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'get_audio_clip_info',
    description: `Get detailed information about an audio clip asset.

**Returns:**
- Duration, channels, sample rate
- Compression format, load type
- File size

**Use Cases:**
- "How long is this sound effect?"
- "What format is this audio?"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        assetPath: {
          type: 'string',
          description: 'Path to the audio clip asset',
        },
      },
      required: ['assetPath'] as string[],
    },
  },
  {
    name: 'list_audio_clips',
    description: `List all audio clips in the project.

**Use Cases:**
- "What sounds are in the project?"
- Finding audio assets`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        folder: {
          type: 'string',
          description: 'Folder to search. Default: "Assets"',
          default: 'Assets',
        },
        nameFilter: {
          type: 'string',
          description: 'Filter by name (partial match)',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results. Default: 100',
          default: 100,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'preview_audio',
    description: `Preview an audio clip in the Unity Editor.

**Actions:**
- "play" - Start playback
- "stop" - Stop playback
- "pause" - Pause playback

**Use Cases:**
- "Play the jump sound"
- Previewing audio without entering Play mode`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        assetPath: {
          type: 'string',
          description: 'Path to the audio clip asset',
        },
        action: {
          type: 'string',
          enum: ['play', 'stop', 'pause'],
          description: 'Playback action',
        },
      },
      required: ['assetPath', 'action'] as string[],
    },
  },
  {
    name: 'set_audio_source',
    description: `Modify AudioSource settings or control playback.

**Can modify:**
- Volume, pitch, loop, mute

**Playback (Play mode only):**
- play, stop, pause

**Use Cases:**
- "Set the volume to 0.5"
- "Make it loop"
- "Play this audio source"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with AudioSource',
        },
        volume: {
          type: 'number',
          description: 'Volume (0-1)',
        },
        pitch: {
          type: 'number',
          description: 'Pitch multiplier',
        },
        loop: {
          type: 'boolean',
          description: 'Loop setting',
        },
        mute: {
          type: 'boolean',
          description: 'Mute setting',
        },
        action: {
          type: 'string',
          enum: ['play', 'stop', 'pause'],
          description: 'Playback action (Play mode only)',
        },
      },
      required: ['gameObjectPath'] as string[],
    },
  },
];

export async function listAudioSources(): Promise<any> {
  const client = getBridgeClient();
  return client.call('list_audio_sources', {});
}

export async function getAudioClipInfo(params: {
  assetPath: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_audio_clip_info', params);
}

export async function listAudioClips(params: {
  folder?: string;
  nameFilter?: string;
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('list_audio_clips', params);
}

export async function previewAudio(params: {
  assetPath: string;
  action: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('preview_audio', params);
}

export async function setAudioSource(params: {
  gameObjectPath: string;
  volume?: number;
  pitch?: number;
  loop?: boolean;
  mute?: boolean;
  action?: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('set_audio_source', params);
}
