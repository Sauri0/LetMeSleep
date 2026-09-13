import copy
import json
import unittest
from integration_receipts import (MAP_IDS, ROLES, LOADING_SUFFIX, LOADING_SCOPE,
                                  VERIFIED, PENDING, validate_receipt, integration_status, receipt_label)


def fixtures():
    common = dict(success=True, unityVersion='6000.3.7f1', configSha256='a'*64, scope='Native limited scope')
    maps = [dict(mapId=i, contentHash='b'*64, spatialSha256='c'*64) for i in sorted(MAP_IDS)]
    catalog = dict(common, declaredCount=5, fiveMapStructuralCheck=True, sceneInstalled=False,
                   outputAssetPath='Assets/Maps.asset', assetGuid='a'*32, assetSha256='d'*64,
                   maps=[dict(r, prefabPath='Assets/'+r['mapId']+'.prefab', spatialHeaderValidated=True, localLightCount=0) for r in maps])
    scene = dict(common, error='', originalPreserved=True, catalogPreserved=True, savedBindingVerified=True,
                 sourceScenePath='Assets/Source.unity', newScenePath='Assets/Copy.unity',
                 sourceGuid='b'*32, newSceneGuid='c'*32, sourceSha256='e'*64, sourceMetaSha256='f'*64,
                 newSceneSha256='1'*64, catalogAssetPath=catalog['outputAssetPath'],
                 catalogGuid=catalog['assetGuid'], catalogSha256=catalog['assetSha256'],
                 maps=[dict(r, prefabGuid='d'*32) for r in maps])
    loading = '\n'.join(['PASS Unity 6000.3.7f1'] + [i+' / '+role+LOADING_SUFFIX for i in sorted(MAP_IDS) for role in ROLES] + [LOADING_SCOPE])
    return dict(catalog=catalog, scene=scene, loading=loading)


def encode(value):
    return (value if isinstance(value, str) else json.dumps(value)).encode()


class NativeReceiptTests(unittest.TestCase):
    def test_scope_drives_post_adjustment_label_without_changing_receipt(self):
        for kind in ('catalog','scene'):
            data=fixtures()[kind]
            self.assertNotIn('postajuste',receipt_label(kind,data))
            for scope in ('Final native readback only.', 'Post-adjustment catalog validation.', 'Validación postajuste.'):
                data['scope']=scope;before=encode(data)
                self.assertTrue(receipt_label(kind,data).endswith('validación postajuste'))
                self.assertEqual(encode(data),before)

    def test_real_schemas_and_complete_gate(self):
        parsed = {k: validate_receipt(k, encode(v)) for k, v in fixtures().items()}
        self.assertEqual(integration_status(parsed), VERIFIED)
        for missing in parsed:
            self.assertEqual(integration_status({k:v for k,v in parsed.items() if k != missing}), PENDING)

    def test_literal_booleans_errors_and_guards(self):
        for kind in ('catalog', 'scene'):
            guards = ['success'] + (['fiveMapStructuralCheck'] if kind == 'catalog' else ['originalPreserved', 'catalogPreserved', 'savedBindingVerified'])
            for field in guards:
                for value in (False, 1, 'true', None):
                    with self.subTest(kind=kind, field=field, value=value):
                        data=fixtures()[kind]; data[field]=value
                        with self.assertRaises(ValueError):validate_receipt(kind, encode(data))
            for value in ('failed', ' ', False, 0, []):
                data=fixtures()[kind]; data['error']=value
                with self.assertRaises(ValueError):validate_receipt(kind, encode(data))
        for value in (True, 0, None):
            data=fixtures()['catalog'];data['sceneInstalled']=value
            with self.assertRaises(ValueError):validate_receipt('catalog',encode(data))

    def test_final_map_ids_and_spatial_evidence(self):
        for kind in ('catalog', 'scene'):
            for mutation in ('duplicate', 'old', 'missing', 'extra', 'hash'):
                with self.subTest(kind=kind, mutation=mutation):
                    data=fixtures()[kind]
                    if mutation=='duplicate':data['maps'][0]=copy.deepcopy(data['maps'][1])
                    elif mutation=='old':data['maps'][0]['mapId']='hf-yate-a-la-deriva-v2'
                    elif mutation=='missing':data['maps'].pop()
                    elif mutation=='extra':data['maps'].append(copy.deepcopy(data['maps'][0]))
                    else:data['maps'][0]['spatialSha256']=''
                    with self.assertRaises(ValueError):validate_receipt(kind,encode(data))
        data=fixtures()['catalog'];data['maps'][0]['spatialHeaderValidated']=1
        with self.assertRaises(ValueError):validate_receipt('catalog',encode(data))

    def test_loading_rejects_incomplete_or_relabelled_evidence(self):
        original=fixtures()['loading']; lines=original.splitlines()
        bad=[original.replace('PASS Unity ', 'PASS\nUnity '), original.replace('PASS.', 'FAIL.',1),
             original.replace('Human:', 'human:',1), original.replace('-v3 /', '-v2 /'),
             '\n'.join(lines[:1]+lines[2:]), '\n'.join(lines[:-1]),
             '\n'.join(lines[:2]+[lines[1]]+lines[3:]), original+'\nPENDING',
             original.replace('6000.3.7f1',''), original+'\n'+lines[1]]
        for text in bad:
            with self.subTest(text=text[:60]):
                with self.assertRaises(ValueError):validate_receipt('loading',encode(text))
        validate_receipt('loading', b'\xef\xbb\xbf'+original.replace('\n','\r\n').encode()+b'\r\n')

    def test_cross_receipt_identity(self):
        for field in ('catalogAssetPath','catalogGuid','catalogSha256','unityVersion','contentHash','spatialSha256'):
            data=fixtures()
            if field in ('contentHash','spatialSha256'):data['scene']['maps'][0][field]='9'*64
            else:data['scene'][field]={'catalogAssetPath':'Assets/Other.asset','catalogGuid':'9'*32,'catalogSha256':'9'*64,'unityVersion':'6000.3.8f1'}[field]
            parsed={k:validate_receipt(k,encode(v)) for k,v in data.items()}
            self.assertEqual(integration_status(parsed),PENDING,field)

    def test_malformed_and_duplicate_json(self):
        for raw in (b'[]', b'{', b'null', b'{"success":false,"success":true}', b'\xff'):
            with self.assertRaises(ValueError):validate_receipt('catalog',raw)
        data=fixtures()['scene'];data['newScenePath']=data['sourceScenePath']
        with self.assertRaises(ValueError):validate_receipt('scene',encode(data))


if __name__ == '__main__':
    unittest.main()
