// Ripped from Magic Software

#include "Include\dRay.h"
#include "dxRay.h"

int Find(const dVector3 Origin, dVector3 Direction, dReal Length, const dVector3 CCPos, const dMatrix3 CCRot, dReal CCRadius, dReal CCLength, dReal T[2]){
	dVector3 U, V, W;
	Decompose(CCRot, U, V, W);

	dVector3 CCOrigin;
	CCOrigin[0] = CCPos[0] - (W[0] * CCLength / 2);
	CCOrigin[1] = CCPos[1] - (W[1] * CCLength / 2);
	CCOrigin[2] = CCPos[2] - (W[2] * CCLength / 2);
	CCOrigin[3] = CCPos[3] - (W[3] * CCLength / 2);
	
	dVector3 D;
	D[0] = dDOT(U, Direction);
	D[1] = dDOT(V, Direction);
	D[2] = dDOT(W, Direction);

	dReal DMag = Length;
	dReal InvDMag = REAL(1.0) / DMag;

	dVector3 Diff;
	Diff[0] = Origin[0] - CCOrigin[0];
	Diff[1] = Origin[1] - CCOrigin[1];
	Diff[2] = Origin[2] - CCOrigin[2];
	Diff[3] = Origin[3] - CCOrigin[3];

	dVector3 P;
	P[0] = dDOT(U, Diff);
	P[1] = dDOT(V, Diff);
	P[2] = dDOT(W, Diff);

	dReal CCRadiusSq = CCRadius * CCRadius;

	dReal Epsilon = 1e-12f;

	if (dFabs(D[2]) >= REAL(1.0) - Epsilon){	// line is parallel to capsule axis
		dReal Discr =  CCRadiusSq - P[0] * P[0] - P[1] * P[1];

		if (Discr >= REAL(0.0)){
            dReal Root = dSqrt(Discr);
            T[0] = (-P[2] + Root) * InvDMag;
            T[1] = (CCLength - P[2] + Root) * InvDMag;
            return 2;
        }
        else return 0;
	}

	// test intersection with infinite cylinder
    dReal A = D[0] * D[0] + D[1] * D[1];
    dReal B = P[0] * D[0] + P[1] * D[1];
    dReal C = P[0] * P[0] + P[1] * P[1] - CCRadiusSq;
    dReal Discr = B * B - A * C;
    if (Discr < REAL(0.0)){	// line does not intersect infinite cylinder
        return 0;
    }

	int Count = 0;

    if (Discr > REAL(0.0)){	// line intersects infinite cylinder in two places
        dReal Root = dSqrt(Discr);
        dReal Inv = REAL(1.0) / A;

		dReal TTemp = (-B - Root) * Inv;

        dReal Tmp = P[2] + TTemp * D[2];
        if (REAL(0.0) <= Tmp && Tmp <= CCLength){
			T[Count++] = TTemp * InvDMag;
		}
		
		
        TTemp = (-B + Root) * Inv;
        Tmp = P[2] + TTemp * D[2];
        if (REAL(0.0) <= Tmp && Tmp <= CCLength){
			T[Count++] = TTemp * InvDMag;
		}

        if (Count == 2){	// line intersects capsule wall in two places
            return 2;
        }
    }
	else{	// line is tangent to infinite cylinder
        dReal TTemp = -B / A;
        dReal Tmp = P[2] + TTemp * D[2];
        if (REAL(0.0) <= Tmp && Tmp <= CCLength){
            T[0] = TTemp * InvDMag;
            return 1;
        }
    }

	// test intersection with bottom hemisphere
    // fA = 1
    B += P[2] * D[2];
    C += P[2] * P[2];
    Discr = B * B - C;
    if (Discr > REAL(0.0)){
        dReal Root = dSqrt(Discr);
        dReal TTemp = -B - Root;
        dReal Tmp = P[2] + TTemp * D[2];
        if (Tmp <= REAL(0.0)){
            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }

        TTemp = -B + Root;
        Tmp = P[2] + TTemp * D[2];
        if (Tmp <= REAL(0.0)){
            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }
    }
    else if (Discr == REAL(0.0)){
        dReal TTemp = -B;
        dReal Tmp = P[2] + TTemp * D[2];
        if (Tmp <= REAL(0.0)){
            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }
    }

	// test intersection with top hemisphere
    // fA = 1
    B -= D[2] * CCLength;
    C += CCLength * (CCLength - REAL(2.0) * P[2]);

    Discr = B * B - C;
    if (Discr > REAL(0.0)){
        dReal Root = dSqrt(Discr);
        dReal TTemp = -B - Root;
        dReal Tmp = P[2] + TTemp * D[2];
        if (Tmp >= CCLength){

            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }

        TTemp = -B + Root;
        Tmp = P[2] + TTemp * D[2];
        if (Tmp >= CCLength){
            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }
    }
    else if (Discr == REAL(0.0)){
        dReal TTemp = -B;
        dReal Tmp = P[2] + TTemp * D[2];
        if (Tmp >= CCLength){
            T[Count++] = TTemp * InvDMag;
            if (Count == 2){
                return 2;
			}
        }
    }
	return Count;
}

int dCollideCCR(dxGeom* RayGeom, dxGeom* CCGeom, int Flags, dContactGeom* Contacts, int Stride){
	const dVector3& CCPos = *(const dVector3*)dGeomGetPosition(CCGeom);
	const dMatrix3& CCRot = *(const dMatrix3*)dGeomGetRotation(CCGeom);

	dReal CCRadius, CCLength;
	dGeomCCylinderGetParams(CCGeom, &CCRadius, &CCLength);

	dVector3 Origin, Direction;
	dGeomRayGet(RayGeom, Origin, Direction);
	dReal Length = dGeomRayGetLength(RayGeom);

	dReal T[2];
    int Count = Find(Origin, Direction, Length, CCPos, CCRot, CCRadius, CCLength, T);
	int ContactCount = 0;
	for (int i = 0; i < Count; i++){
		if (T[i] >= 0.0){
			dContactGeom* Contact = CONTACT(Flags, Contacts, ContactCount, Stride);
			Contact->pos[0] = Origin[0] + T[i] * Direction[0] * Length;
			Contact->pos[1] = Origin[1] + T[i] * Direction[1] * Length;
			Contact->pos[2] = Origin[2] + T[i] * Direction[2] * Length;
			Contact->pos[3] = Origin[3] + T[i] * Direction[3] * Length;
			//Contact->normal = 0;
			Contact->depth = 0.0f;
			Contact->g1 = RayGeom;
			Contact->g2 = CCGeom;

			ContactCount++;
		}
	}
    return ContactCount;
}