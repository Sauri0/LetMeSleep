"""Validate native receipts without rewriting them or broadening their scope."""
import json
import re

MAP_IDS = frozenset((
    'hf-isla-del-laguito-v2', 'hf-casa-del-patio-v1', 'hf-campamento-pinar-v2',
    'hf-yate-a-la-deriva-v3', 'hf-puerto-del-faro-v1',
))
ROLES = ('Human', 'Mosquito')
LOADING_SUFFIX = ': correct identity, runtime, navigation, three finite actors and return to menu PASS.'
LOADING_SCOPE = 'Ten local training sessions via application API; no WAN, full playthrough, role-capacity or performance claim.'
VERIFIED = 'Integración local verificada'
PENDING = 'Integración Unity en verificación'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def hex_value(data, field, length=64):
    value = data.get(field)
    require(isinstance(value, str) and re.fullmatch('[0-9a-fA-F]{%d}' % length, value), field)


def asset_path(data, field, suffix):
    value = data.get(field)
    require(isinstance(value, str) and value.startswith('Assets/') and value.endswith(suffix)
            and not any(c in value for c in '\\:')
            and all(p and p not in ('.', '..') and p == p.strip() for p in value.split('/')), field)


def validate_receipt(kind, raw):
    """Return parsed native content; invalid evidence raises ValueError."""
    text = raw.decode('utf-8-sig')
    if kind == 'loading':
        lines = text.splitlines()
        require(len(lines) == 12, 'Expected header, ten role lines and native scope')
        match = re.fullmatch(r'PASS Unity (\d+\.\d+\.\d+[A-Za-z][0-9A-Za-z.]*)', lines[0])
        require(match is not None, 'Native Unity PASS header missing')
        expected = {map_id + ' / ' + role + LOADING_SUFFIX for map_id in MAP_IDS for role in ROLES}
        require(set(lines[1:11]) == expected and len(set(lines[1:11])) == 10, 'Ten exact final map/role PASS lines required')
        require(lines[11] == LOADING_SCOPE, 'Native loading scope missing or changed')
        return dict(unityVersion=match.group(1), scope=lines[11], mapRoles=sorted(expected))
    require(kind in ('catalog', 'scene'), 'Unknown receipt kind')
    data = json.loads(text, object_pairs_hook=unique_object)
    require(isinstance(data, dict), 'Receipt must be an object')
    require(data.get('success') is True, 'Literal success:true required')
    require(data.get('error') is None or data.get('error') == '', 'Nonempty error')
    require(not data.get('errors') and not data.get('pending'), 'Errors or pending evidence')
    require(isinstance(data.get('scope'), str) and data['scope'].strip(), 'Native scope missing')
    require(isinstance(data.get('unityVersion'), str) and re.fullmatch(r'\d+\.\d+\.\d+[A-Za-z][0-9A-Za-z.]*', data['unityVersion']), 'Unity version missing')
    hex_value(data, 'configSha256')
    rows = data.get('maps')
    require(isinstance(rows, list) and len(rows) == 5 and all(isinstance(row, dict) for row in rows), 'Five map records required')
    ids = [row.get('mapId') for row in rows]
    require(all(isinstance(i, str) for i in ids) and set(ids) == MAP_IDS, 'Final five unique IDs required')
    for row in rows:
        hex_value(row, 'contentHash')
        hex_value(row, 'spatialSha256')
        if kind == 'catalog':
            require(row.get('spatialHeaderValidated') is True, 'Spatial header guard')
            require(type(row.get('localLightCount')) is int and row['localLightCount'] >= 0, 'Local light count')
            asset_path(row, 'prefabPath', '.prefab')
        else:
            hex_value(row, 'prefabGuid', 32)
    if kind == 'catalog':
        require(type(data.get('declaredCount')) is int and data['declaredCount'] == 5, 'Declared five-map count')
        require(data.get('fiveMapStructuralCheck') is True, 'Structural guard')
        require(data.get('sceneInstalled') is False, 'Catalog must preserve authoring-only scope')
        asset_path(data, 'outputAssetPath', '.asset')
        hex_value(data, 'assetGuid', 32)
        hex_value(data, 'assetSha256')
    else:
        for guard in ('originalPreserved', 'catalogPreserved', 'savedBindingVerified'):
            require(data.get(guard) is True, guard)
        for field in ('sourceGuid', 'newSceneGuid', 'catalogGuid'):
            hex_value(data, field, 32)
        for field in ('sourceSha256', 'sourceMetaSha256', 'newSceneSha256', 'catalogSha256'):
            hex_value(data, field)
        for field in ('sourceScenePath', 'newScenePath'):
            asset_path(data, field, '.unity')
        asset_path(data, 'catalogAssetPath', '.asset')
        require(data['sourceScenePath'].casefold() != data['newScenePath'].casefold(), 'New scene path required')
        require(data['sourceGuid'].lower() != data['newSceneGuid'].lower(), 'New scene GUID required')
    return data


def integration_status(receipts):
    """All three validated receipts must agree before publishing local verification."""
    if set(receipts) != {'catalog', 'scene', 'loading'}:
        return PENDING
    catalog, scene, loading = (receipts[k] for k in ('catalog', 'scene', 'loading'))
    if len({r['unityVersion'] for r in (catalog, scene, loading)}) != 1:
        return PENDING
    for left, right in (('outputAssetPath', 'catalogAssetPath'), ('assetGuid', 'catalogGuid'), ('assetSha256', 'catalogSha256')):
        if catalog[left].lower() != scene[right].lower():
            return PENDING
    scene_maps = {row['mapId']: row for row in scene['maps']}
    for row in catalog['maps']:
        if any(row[field].lower() != scene_maps[row['mapId']][field].lower() for field in ('contentHash', 'spatialSha256')):
            return PENDING
    return VERIFIED
