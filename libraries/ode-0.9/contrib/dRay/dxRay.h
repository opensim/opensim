struct dxRay{
	dReal Length;
};

inline void Decompose(const dMatrix3 Matrix, dVector3 Right, dVector3 Up, dVector3 Direction){
	Right[0] = Matrix[0 * 4 + 0];
	Right[1] = Matrix[1 * 4 + 0];
	Right[2] = Matrix[2 * 4 + 0];
	Right[3] = Matrix[3 * 4 + 0];
	Up[0] = Matrix[0 * 4 + 1];
	Up[1] = Matrix[1 * 4 + 1];
	Up[2] = Matrix[2 * 4 + 1];
	Up[3] = Matrix[3 * 4 + 1];
	Direction[0] = Matrix[0 * 4 + 2];
	Direction[1] = Matrix[1 * 4 + 2];
	Direction[2] = Matrix[2 * 4 + 2];
	Direction[3] = Matrix[3 * 4 + 2];
}

inline void Decompose(const dMatrix3 Matrix, dVector3 Vectors[3]){
	Decompose(Matrix, Vectors[0], Vectors[1], Vectors[2]);
}

inline dContactGeom* CONTACT(int Flags, dContactGeom* Contacts, int Index, int Stride){
	dIASSERT(Index >= 0 && Index < (Flags & 0x0ffff));
	return ((dContactGeom*)(((char*)Contacts) + (Index * Stride)));
}

int dCollidePR(dxGeom* RayGeom, dxGeom* PlaneGeom, int Flags, dContactGeom* Contacts, int Stride);
int dCollideSR(dxGeom* RayGeom, dxGeom* SphereGeom, int Flags, dContactGeom* Contacts, int Stride);
int dCollideBR(dxGeom* RayGeom, dxGeom* BoxGeom, int Flags, dContactGeom* Contacts, int Stride);
int dCollideCCR(dxGeom* RayGeom, dxGeom* CCylinderGeom, int Flags, dContactGeom* Contacts, int Stride);